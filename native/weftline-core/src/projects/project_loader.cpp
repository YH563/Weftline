#include "weftline/projects/project_loader.hpp"

#include <algorithm>
#include <memory>
#include <string>
#include <string_view>
#include <utility>
#include <variant>
#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/projects/compilation_cache.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace weftline::projects {
    namespace {

        [[nodiscard]] bool ends_with_ignore_case(
            std::string_view text, std::string_view suffix) noexcept
        {
            if (text.size() < suffix.size()) {
                return false;
            }

            const auto offset = text.size() - suffix.size();
            for (std::size_t index = 0; index < suffix.size(); ++index) {
                auto left = static_cast<unsigned char>(text[offset + index]);
                auto right = static_cast<unsigned char>(suffix[index]);
                if (left >= 'A' && left <= 'Z') {
                    left = static_cast<unsigned char>(left - 'A' + 'a');
                }
                if (right >= 'A' && right <= 'Z') {
                    right = static_cast<unsigned char>(right - 'A' + 'a');
                }
                if (left != right) {
                    return false;
                }
            }

            return true;
        }

    } // namespace

    LoadedProject load(const ProjectSnapshot& snapshot, CompilationCache* cache)
    {
        LoadedProject project;
        project.folder = snapshot.folder;

        std::vector<std::string> weft_paths;
        std::string wstory_path;

        for (const auto& [path, text] : snapshot.documents) {
            static_cast<void>(text);
            if (ends_with_ignore_case(path, ".weft")) {
                weft_paths.push_back(path);
            } else if (ends_with_ignore_case(path, ".wstory")) {
                if (wstory_path.empty() || path < wstory_path) {
                    wstory_path = path;
                }
            }
        }

        std::sort(weft_paths.begin(), weft_paths.end());

        for (const auto& path : weft_paths) {
            const auto& text = snapshot.documents.at(path);

            if (cache != nullptr) {
                if (const auto* cached = cache->find_weft(path, text)) {
                    if (cached->ast) {
                        project.weft_files.push_back(ProjectFile{path, cached->ast});
                    }
                    for (const auto& diagnostic : cached->diagnostics) {
                        project.diagnostics.push_back(
                            ProjectDiagnostic{path, diagnostic});
                    }
                    continue;
                }
            }

            const auto source =
                source::SourceText::from_utf8(text, std::string(path));
            diagnostics::DiagnosticBag bag;
            auto script = syntax::parse(source, bag);

            std::vector<diagnostics::Diagnostic> parse_diagnostics(
                bag.begin(), bag.end());
            for (const auto& diagnostic : parse_diagnostics) {
                project.diagnostics.push_back(
                    ProjectDiagnostic{path, diagnostic});
            }

            std::shared_ptr<const syntax::WeftFile> ast;
            if (auto* weft = std::get_if<syntax::WeftFile>(&script)) {
                ast = std::make_shared<const syntax::WeftFile>(std::move(*weft));
            }

            if (cache != nullptr) {
                cache->set_weft(path, text, ast, std::move(parse_diagnostics));
            }

            if (ast) {
                project.weft_files.push_back(ProjectFile{path, std::move(ast)});
            }
        }

        if (!wstory_path.empty()) {
            const auto& text = snapshot.documents.at(wstory_path);

            if (cache != nullptr) {
                if (const auto* cached = cache->find_wstory(wstory_path, text)) {
                    if (cached->ast) {
                        project.wstory = *cached->ast;
                        project.wstory_path = wstory_path;
                    }
                    for (const auto& diagnostic : cached->diagnostics) {
                        project.diagnostics.push_back(
                            ProjectDiagnostic{wstory_path, diagnostic});
                    }
                    return project;
                }
            }

            const auto source =
                source::SourceText::from_utf8(text, std::string(wstory_path));
            diagnostics::DiagnosticBag bag;
            auto script = syntax::parse(source, bag);

            std::vector<diagnostics::Diagnostic> parse_diagnostics(
                bag.begin(), bag.end());
            for (const auto& diagnostic : parse_diagnostics) {
                project.diagnostics.push_back(
                    ProjectDiagnostic{wstory_path, diagnostic});
            }

            std::shared_ptr<const syntax::WStoryFile> ast;
            if (auto* wstory = std::get_if<syntax::WStoryFile>(&script)) {
                ast = std::make_shared<const syntax::WStoryFile>(std::move(*wstory));
            }

            if (cache != nullptr) {
                cache->set_wstory(
                    wstory_path, text, ast, std::move(parse_diagnostics));
            }

            if (ast) {
                project.wstory = *ast;
                project.wstory_path = wstory_path;
            }
        }

        return project;
    }

} // namespace weftline::projects
