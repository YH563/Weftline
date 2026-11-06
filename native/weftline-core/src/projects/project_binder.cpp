#include "weftline/projects/project_binder.hpp"

#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <utility>
#include <vector>

#include "weftline/binding/binder.hpp"

namespace weftline::projects {
    namespace {

        void declare_chapter(
            const ProjectFile& file,
            std::unordered_map<std::u16string, ChapterDefinition>& chapters,
            std::vector<ProjectDiagnostic>& out)
        {
            const auto& ast = *file.ast;
            const auto found = chapters.find(ast.title);
            if (found != chapters.end()) {
                std::u16string message = u"章节标题重复：";
                message += ast.title;
                out.push_back(ProjectDiagnostic{
                    file.file_path,
                    diagnostics::Diagnostic{
                        std::string(diagnostics::DiagnosticCode::DuplicateChapterTitle),
                        diagnostics::Severity::Error,
                        std::move(message),
                        ast.title_span,
                        source::SourceLocation{
                            found->second.file_path, found->second.title_span},
                    },
                });
                return;
            }

            chapters.emplace(
                ast.title,
                ChapterDefinition{
                    ast.title, file.file_path, ast.span, ast.title_span});
        }

        void declare_symbol(
            const ProjectFile& file,
            binding::SymbolTable& symbols,
            std::vector<ProjectDiagnostic>& out,
            std::u16string name,
            binding::SymbolKind kind,
            std::optional<bool> value,
            source::TextSpan span)
        {
            binding::SymbolEntry entry{
                std::move(name),
                kind,
                source::SourceLocation{file.file_path, span},
                value,
            };

            const auto entry_name = entry.name;
            if (symbols.try_declare(std::move(entry))) {
                return;
            }

            const auto* existing = symbols.find(entry_name);
            std::u16string message;

            if (existing != nullptr && existing->kind == kind) {
                message = u"全局";
                message += binding::kind_name(kind);
                message += u"重复声明：";
                message += entry_name;
            } else {
                message = u"符号名已被声明为";
                message += existing != nullptr
                    ? binding::kind_name(existing->kind)
                    : u"符号";
                message += u"：";
                message += entry_name;
            }

            std::optional<source::SourceLocation> related;
            if (existing != nullptr) {
                related = existing->location;
            }

            out.push_back(ProjectDiagnostic{
                file.file_path,
                diagnostics::Diagnostic{
                    std::string(existing != nullptr && existing->kind == kind
                        ? binding::duplicate_code(kind)
                        : diagnostics::DiagnosticCode::DuplicateSymbol),
                    diagnostics::Severity::Error,
                    std::move(message),
                    span,
                    std::move(related),
                },
            });
        }

        void declare_symbols(
            const ProjectFile& file,
            binding::SymbolTable& symbols,
            std::vector<ProjectDiagnostic>& out)
        {
            for (const auto& variable : file.ast->variables) {
                declare_symbol(
                    file, symbols, out, variable.name,
                    binding::SymbolKind::Variable, variable.value, variable.span);
            }
            for (const auto& character : file.ast->characters) {
                declare_symbol(
                    file, symbols, out, character.name,
                    binding::SymbolKind::Character, std::nullopt, character.span);
            }
            for (const auto& item : file.ast->items) {
                declare_symbol(
                    file, symbols, out, item.name,
                    binding::SymbolKind::Item, std::nullopt, item.span);
            }
            for (const auto& scene : file.ast->scenes) {
                declare_symbol(
                    file, symbols, out, scene.name,
                    binding::SymbolKind::Scene, std::nullopt, scene.span);
            }
        }

        void declare_modules(
            const ProjectFile& file,
            std::unordered_map<std::u16string, ModuleDefinition>& modules,
            std::vector<ProjectDiagnostic>& out)
        {
            for (const auto& module : file.ast->modules) {
                const auto found = modules.find(module.title);
                if (found != modules.end()) {
                    if (found->second.file_path == file.file_path) {
                        continue;
                    }

                    std::u16string message = u"模块 ID 重复：";
                    message += module.title;
                    out.push_back(ProjectDiagnostic{
                        file.file_path,
                        diagnostics::Diagnostic{
                            std::string(diagnostics::DiagnosticCode::DuplicateModuleTitle),
                            diagnostics::Severity::Error,
                            std::move(message),
                            module.title_span,
                            source::SourceLocation{
                                found->second.file_path, found->second.title_span},
                        },
                    });
                    continue;
                }

                modules.emplace(
                    module.title,
                    ModuleDefinition{
                        module.title,
                        file.ast->title,
                        file.file_path,
                        module.span,
                        module.title_span,
                        module.scene,
                    });
            }
        }

    } // namespace

    ProjectBindingResult bind(
        std::vector<ProjectFile> files,
        const syntax::WStoryFile* wstory,
        std::string_view wstory_path)
    {
        std::vector<ProjectDiagnostic> out;
        binding::SymbolTable symbols;
        std::unordered_map<std::u16string, ChapterDefinition> chapters;
        std::unordered_map<std::u16string, ModuleDefinition> modules;

        for (const auto& file : files) {
            declare_chapter(file, chapters, out);
            declare_symbols(file, symbols, out);
            declare_modules(file, modules, out);
        }

        if (wstory != nullptr) {
            diagnostics::DiagnosticBag bag;
            binding::bind(*wstory, bag, wstory_path);
            for (const auto& diagnostic : bag) {
                out.push_back(ProjectDiagnostic{
                    std::string(wstory_path), diagnostic});
            }
        }

        for (const auto& file : files) {
            for (const auto& module : file.ast->modules) {
                diagnostics::DiagnosticBag bag;
                binding::bind_module(module, symbols, bag);
                for (const auto& diagnostic : bag) {
                    out.push_back(ProjectDiagnostic{
                        file.file_path, diagnostic});
                }
            }
        }

        ProjectIndex index{
            symbols.symbols(),
            std::move(chapters),
            std::move(modules),
            std::move(files),
        };

        return ProjectBindingResult{std::move(index), std::move(out)};
    }

} // namespace weftline::projects
