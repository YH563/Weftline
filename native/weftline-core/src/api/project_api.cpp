#include "weftline/core.h"

#include <cstdlib>
#include <cstring>
#include <exception>
#include <new>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#include "weftline/api/compile.hpp"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/ir/ir_json.hpp"
#include "weftline/json/json_writer.hpp"
#include "weftline/projects/compilation_cache.hpp"
#include "weftline/projects/project_compiler.hpp"
#include "weftline/projects/project_snapshot.hpp"
#include "weftline/source/source_text.hpp"

struct WeftProject
{
    weftline::projects::ProjectSnapshot snapshot;
    weftline::projects::CompilationCache cache;
};

namespace {

    [[nodiscard]] std::string project_error_json(std::string_view message)
    {
        weftline::json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(2);
        writer.key("version").string(weft_version());
        writer.key("hasErrors").boolean(true);
        writer.key("error").string(message);
        writer.end_object();
        return writer.str();
    }

    [[nodiscard]] std::string project_compile_json(
        const weftline::projects::ProjectCompilationResult& result)
    {
        weftline::json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(2);
        writer.key("version").string(weft_version());
        writer.key("hasErrors").boolean(result.has_errors());
        writer.key("projectName").string(
            weftline::source::utf16_to_utf8(result.model.project.name));

        writer.key("diagnostics").begin_array();
        for (const auto& item : result.diagnostics) {
            const auto& diagnostic = item.diagnostic;
            writer.begin_object();
            writer.key("code").string(diagnostic.code);
            writer.key("severity").string(
                std::string(weftline::diagnostics::severity_name(diagnostic.severity)));
            writer.key("message").string(
                weftline::source::utf16_to_utf8(diagnostic.message));
            writer.key("filePath").string(item.file_path);
            writer.key("start").number(diagnostic.span.start);
            writer.key("length").number(diagnostic.span.length);

            if (diagnostic.related) {
                writer.key("related").begin_object();
                writer.key("filePath").string(diagnostic.related->file_path);
                writer.key("start").number(diagnostic.related->span.start);
                writer.key("length").number(diagnostic.related->span.length);
                writer.end_object();
            }

            writer.end_object();
        }
        writer.end_array();

        if (result.graph) {
            writer.key("graph").raw(
                weftline::ir::story_graph_to_json(*result.graph));
        }

        if (result.projection) {
            writer.key("projection").raw(
                weftline::ir::graph_projection_to_json(*result.projection));
        }

        writer.end_object();
        return writer.str();
    }

    [[nodiscard]] char* duplicate_string(const std::string& value)
    {
        auto* result = static_cast<char*>(std::malloc(value.size() + 1));
        if (result == nullptr) {
            return nullptr;
        }
        std::memcpy(result, value.c_str(), value.size() + 1);
        return result;
    }

} // namespace

extern "C" {

WeftProject* weft_project_create(void)
{
    return new (std::nothrow) WeftProject{};
}

void weft_project_destroy(WeftProject* project)
{
    delete project;
}

void weft_project_set_folder(WeftProject* project, const char* folder)
{
    if (project == nullptr || folder == nullptr) {
        return;
    }
    if (project->snapshot.folder != folder) {
        project->cache.clear();
    }
    project->snapshot.folder = folder;
}

void weft_project_set_document(WeftProject* project, const char* path, const char* text)
{
    if (project == nullptr || path == nullptr) {
        return;
    }
    project->snapshot.documents[path] = text != nullptr ? text : "";
    ++project->snapshot.version;
}

void weft_project_remove_document(WeftProject* project, const char* path)
{
    if (project == nullptr || path == nullptr) {
        return;
    }
    project->snapshot.documents.erase(path);
    ++project->snapshot.version;
}

char* weft_project_compile(WeftProject* project, int allow_partial, int build_projection)
{
    try {
        if (project == nullptr) {
            return duplicate_string(project_error_json("project is null"));
        }

        weftline::projects::CompilationOptions options;
        options.allow_partial = allow_partial != 0;
        options.build_projection = build_projection != 0;

        const auto result =
            weftline::projects::compile(
                project->snapshot, options, &project->cache);

        return duplicate_string(project_compile_json(result));
    } catch (const std::exception& error) {
        return duplicate_string(project_error_json(error.what()));
    } catch (...) {
        return duplicate_string(project_error_json("unknown native error"));
    }
}

} // extern "C"
