#include "weftline/projects/project_binder.hpp"

#include <cstddef>
#include <functional>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

#include "weftline/binding/binder.hpp"
#include "weftline/projects/compilation_cache.hpp"

namespace weftline::projects {
    namespace {

        // 跨文件建立章节表；重复标题只在重复出现的文件上报错，related 指向首次声明。
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

        // 跨文件声明符号；同名不同类型也视为重复。
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

                std::vector<std::u16string> scenes;
                scenes.reserve(module.scenes.size());
                for (const auto& scene : module.scenes) {
                    scenes.push_back(scene.name);
                }

                modules.emplace(
                    module.title,
                    ModuleDefinition{
                        module.title,
                        file.ast->title,
                        file.file_path,
                        module.span,
                        module.title_span,
                        std::move(scenes),
                    });
            }
        }

        // 全局声明指纹：只关心名字和种类。签名不变时模块引用诊断仍有效。
        [[nodiscard]] std::size_t declaration_signature(
            const std::vector<ProjectFile>& files)
        {
            std::size_t hash = 1469598103934665603ULL;
            const auto mix = [&hash](std::size_t value) {
                hash ^= value;
                hash *= 1099511628211ULL;
            };
            const auto mix_name = [&mix](const std::u16string& name) {
                mix(std::hash<std::u16string>{}(name));
            };

            for (const auto& file : files) {
                for (const auto& variable : file.ast->variables) {
                    mix_name(variable.name);
                    mix(static_cast<std::size_t>(binding::SymbolKind::Variable));
                }
                for (const auto& character : file.ast->characters) {
                    mix_name(character.name);
                    mix(static_cast<std::size_t>(binding::SymbolKind::Character));
                }
                for (const auto& item : file.ast->items) {
                    mix_name(item.name);
                    mix(static_cast<std::size_t>(binding::SymbolKind::Item));
                }
                for (const auto& scene : file.ast->scenes) {
                    mix_name(scene.name);
                    mix(static_cast<std::size_t>(binding::SymbolKind::Scene));
                }
            }

            return hash;
        }

        // 单文件模块绑定；生成诊断时保留该文件路径，方便后续按文件归属。
        [[nodiscard]] std::vector<ProjectDiagnostic> bind_modules(
            const ProjectFile& file,
            binding::SymbolTable& symbols)
        {
            std::vector<ProjectDiagnostic> out;

            for (const auto& module : file.ast->modules) {
                diagnostics::DiagnosticBag bag;
                binding::bind_module(module, symbols, bag);
                for (const auto& diagnostic : bag) {
                    out.push_back(ProjectDiagnostic{file.file_path, diagnostic});
                }
            }

            return out;
        }

    } // namespace

    ProjectBindingResult bind(
        std::vector<ProjectFile> files,
        const syntax::WStoryFile* wstory,
        std::string_view wstory_path,
        BindingCache* cache)
    {
        std::vector<ProjectDiagnostic> out;
        binding::SymbolTable symbols;
        std::unordered_map<std::u16string, ChapterDefinition> chapters;
        std::unordered_map<std::u16string, ModuleDefinition> modules;

        // 第一阶段：所有文件的声明先合并，保证前向引用可见。
        for (const auto& file : files) {
            declare_chapter(file, chapters, out);
            declare_symbols(file, symbols, out);
            declare_modules(file, modules, out);
        }

        // .wstory 只做自身重复检查；它不会把声明加入符号表。
        if (wstory != nullptr) {
            diagnostics::DiagnosticBag bag;
            binding::bind(*wstory, bag, wstory_path);
            for (const auto& diagnostic : bag) {
                out.push_back(ProjectDiagnostic{
                    std::string(wstory_path), diagnostic});
            }
        }

        // 第二阶段：模块引用。符号签名不变时，未变化文件可复用旧诊断。
        const auto signature = declaration_signature(files);
        const bool can_reuse = cache != nullptr
            && cache->has_symbol_signature
            && cache->symbol_signature == signature;
        std::unordered_set<std::string> active_files;

        for (const auto& file : files) {
            active_files.insert(file.file_path);

            // AST 指针相同说明该文件文本没变；只有签名相同才允许走复用。
            if (can_reuse) {
                const auto found = cache->files.find(file.file_path);
                if (found != cache->files.end()
                    && found->second.ast.lock() == file.ast) {
                    out.insert(
                        out.end(),
                        found->second.diagnostics.begin(),
                        found->second.diagnostics.end());
                    continue;
                }
            }

            auto diagnostics = bind_modules(file, symbols);
            out.insert(out.end(), diagnostics.begin(), diagnostics.end());

            if (cache != nullptr) {
                cache->files[file.file_path] = CachedModuleBinding{
                    file.ast, std::move(diagnostics)};
            }
        }

        // 删除已经不在项目里的缓存项，避免项目缩小时保留无用 AST。
        if (cache != nullptr) {
            for (auto it = cache->files.begin(); it != cache->files.end();) {
                if (active_files.find(it->first) == active_files.end()) {
                    it = cache->files.erase(it);
                } else {
                    ++it;
                }
            }

            cache->symbol_signature = signature;
            cache->has_symbol_signature = true;
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
