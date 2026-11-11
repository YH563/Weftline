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

/**
 * @brief C ABI 项目句柄：持有文档快照与跨次编译缓存。
 *
 * 句柄不是线程安全的；文档文本由调用方通过 set_document 传入，core 不访问磁盘。
 * 缓存包含文件级 AST、选择性绑定结果和图/投影；切换文件夹时必须清空。
 */
struct WeftProject
{
    weftline::projects::ProjectSnapshot snapshot;
    weftline::projects::CompilationCache cache;
};

namespace {

    /**
     * @brief 构造项目编译失败时的最小 JSON 响应。
     *
     * 保持与正常响应相同的 schemaVersion/version/hasErrors 字段，方便 C# 统一解析。
     */
    [[nodiscard]] std::string project_error_json(std::string_view message)
    {
        weftline::json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(3);
        writer.key("version").string(weft_version());
        writer.key("hasErrors").boolean(true);
        writer.key("error").string(message);
        writer.end_object();
        return writer.str();
    }

    /**
     * @brief 写出一组声明符号，并按名字回查定义位置。
     *
     * 位置以 UTF-16 code unit 计量；找不到定义时省略 filePath/start/length。
     * @param write_extra 用于输出该符号类型特有的字段（例如变量 initial）。
     */
    template <typename Entry, typename WriteExtra>
    void write_symbol_array(
        weftline::json::Writer& writer,
        const weftline::projects::ProjectIndex& index,
        const std::vector<Entry>& entries,
        WriteExtra&& write_extra)
    {
        writer.begin_array();

        for (const auto& entry : entries) {
            writer.begin_object();
            writer.key("name").string(
                weftline::source::utf16_to_utf8(entry.name));
            write_extra(writer, entry);

            if (entry.description) {
                writer.key("description").string(
                    weftline::source::utf16_to_utf8(*entry.description));
            }

            const auto found = index.symbols.find(entry.name);
            if (found != index.symbols.end()) {
                writer.key("filePath").string(found->second.location.file_path);
                writer.key("start").number(found->second.location.span.start);
                writer.key("length").number(found->second.location.span.length);
            }

            writer.end_object();
        }

        writer.end_array();
    }

    /**
     * @brief 输出 C# 编辑器需要的变量/角色/道具/场景/章节及描述。
     *
     * .wstory 只提供描述，不改变声明来源；声明与定义位置来自 .weft 索引。
     */
    void write_project_symbols(
        weftline::json::Writer& writer,
        const weftline::projects::ProjectCompilationResult& result)
    {
        const auto& index = result.index;
        const auto& model = result.model;

        writer.begin_object();

        writer.key("variables");
        write_symbol_array(
            writer, index, model.symbols.variables,
            [](weftline::json::Writer& output, const auto& variable) {
                output.key("initial").boolean(variable.initial);
            });

        writer.key("characters");
        write_symbol_array(
            writer, index, model.symbols.characters,
            [](weftline::json::Writer&, const auto&) {});

        writer.key("items");
        write_symbol_array(
            writer, index, model.symbols.items,
            [](weftline::json::Writer&, const auto&) {});

        writer.key("scenes");
        write_symbol_array(
            writer, index, model.symbols.scenes,
            [](weftline::json::Writer&, const auto&) {});

        writer.key("chapters").begin_array();
        for (const auto& chapter : model.chapters) {
            writer.begin_object();
            writer.key("id").string(
                weftline::source::utf16_to_utf8(chapter.id));
            writer.key("title").string(
                weftline::source::utf16_to_utf8(chapter.title));

            if (chapter.description) {
                writer.key("description").string(
                    weftline::source::utf16_to_utf8(*chapter.description));
            }

            writer.key("moduleIds").begin_array();
            for (const auto& module_id : chapter.module_ids) {
                writer.string(weftline::source::utf16_to_utf8(module_id));
            }
            writer.end_array();

            writer.key("filePath").string(chapter.source.file_path);
            writer.key("start").number(chapter.source.span.start);
            writer.key("length").number(chapter.source.span.length);
            writer.end_object();
        }
        writer.end_array();

        writer.end_object();
    }

    /**
     * @brief 把一次项目编译结果序列化为 C# 使用的 JSON（schema v3）。
     *
     * 响应包含诊断、符号、可选的 graph/projection。graph/projection 可能来自
     * CompilationCache 的复用结果，因此这里只做序列化，不再复制 IR。
     * 所有 start/length 都是 UTF-16 code unit。
     */
    [[nodiscard]] std::string project_compile_json(
        const weftline::projects::ProjectCompilationResult& result)
    {
        weftline::json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(3);
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

        writer.key("symbols");
        write_project_symbols(writer, result);

        if (result.graph) {
            writer.key("graph").raw(
                weftline::ir::story_graph_to_json(*result.graph.value()));
        }

        if (result.projection) {
            writer.key("projection").raw(
                weftline::ir::graph_projection_to_json(*result.projection.value()));
        }

        writer.end_object();
        return writer.str();
    }

    /**
     * @brief 用 malloc 复制字符串，交给 C ABI 调用方。
     *
     * 返回的缓冲区必须由调用方使用 weft_free() 释放；分配失败返回 nullptr。
     */
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

// C ABI 语义见 include/weftline/core.h；以下实现只补充生命周期和缓存约束。

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
    // 缓存 key 是文档路径；换项目后旧 AST/绑定/图结果不可复用。
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
    // 文本是内存快照，不由 core 访问磁盘；版本号用于最新的编译结果判定。
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
    // 不向 C ABI 抛异常：所有失败都转换成可解析的 JSON 响应。
    try {
        if (project == nullptr) {
            return duplicate_string(project_error_json("project is null"));
        }

        weftline::projects::CompilationOptions options;
        options.allow_partial = allow_partial != 0;
        options.build_projection = build_projection != 0;

        // 缓存随句柄存活：未变化文件复用 AST/绑定/图结果，变化部分重新编译。
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
