#include "weftline/projects/validator.hpp"

#include <deque>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/projects/project_entry.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::projects {
    namespace {

        using ModuleMap =
            std::unordered_map<std::u16string, const syntax::Module*>;

        // 只保留“真正属于该文件”的模块，避免同名模块跨文件时指针错位。
        [[nodiscard]] ModuleMap build_syntax_map(
            const LoadedProject& project,
            const ProjectIndex& index)
        {
            ModuleMap map;

            for (const auto& file : project.weft_files) {
                for (const auto& module : file.ast->modules) {
                    const auto found = index.modules.find(module.title);
                    if (found == index.modules.end()) {
                        continue;
                    }
                    if (found->second.file_path != file.file_path) {
                        continue;
                    }
                    if (!(found->second.title_span == module.title_span)) {
                        continue;
                    }
                    map[module.title] = &module;
                }
            }

            return map;
        }

        // 检查所有 -> 目标是否存在；END 是内建汇点，不需要定义。
        void validate_targets(
            const LoadedProject& project,
            const ProjectIndex& index,
            std::vector<ProjectDiagnostic>& out)
        {
            for (const auto& file : project.weft_files) {
                for (const auto& module : file.ast->modules) {
                    if (!module.exit) {
                        continue;
                    }

                    for (const auto& branch : module.exit->branches) {
                        if (branch.target == u"END") {
                            continue;
                        }

                        if (index.modules.find(branch.target) == index.modules.end()) {
                            std::u16string message = u"找不到目标模块：";
                            message += branch.target;
                            out.push_back(ProjectDiagnostic{
                                file.file_path,
                                diagnostics::Diagnostic{
                                    std::string(diagnostics::DiagnosticCode::TargetNotFound),
                                    diagnostics::Severity::Error,
                                    std::move(message),
                                    branch.target_span,
                                    std::nullopt,
                                },
                            });
                        }
                    }
                }
            }
        }

        // 从默认入口和候选入口做 BFS，忽略 END 和不存在的目标模块。
        [[nodiscard]] std::unordered_set<std::u16string> reachable_from(
            const std::vector<std::u16string>& entries,
            const ModuleMap& syntax_by_title,
            const ProjectIndex& index)
        {
            std::unordered_set<std::u16string> visited;
            std::deque<std::u16string> queue;

            for (const auto& entry : entries) {
                if (syntax_by_title.find(entry) != syntax_by_title.end()) {
                    queue.push_back(entry);
                }
            }

            while (!queue.empty()) {
                auto current = std::move(queue.front());
                queue.pop_front();

                if (!visited.insert(current).second) {
                    continue;
                }

                const auto found = syntax_by_title.find(current);
                if (found == syntax_by_title.end()) {
                    continue;
                }

                const auto* module = found->second;
                if (!module->exit) {
                    continue;
                }

                for (const auto& branch : module->exit->branches) {
                    if (index.modules.find(branch.target) != index.modules.end()
                        && visited.find(branch.target) == visited.end()) {
                        queue.push_back(branch.target);
                    }
                }
            }

            return visited;
        }

        // 在反向邻接表上从 END 反推，得到所有能到达结局的模块。
        [[nodiscard]] std::unordered_set<std::u16string> can_reach_end(
            const ModuleMap& syntax_by_title,
            const ProjectIndex& index)
        {
            std::unordered_map<std::u16string, std::unordered_set<std::u16string>> reverse;
            std::unordered_set<std::u16string> reaches;
            std::deque<std::u16string> queue;

            for (const auto& [title, module] : syntax_by_title) {
                if (!module->exit) {
                    continue;
                }

                for (const auto& branch : module->exit->branches) {
                    if (branch.target == u"END") {
                        if (reaches.insert(title).second) {
                            queue.push_back(title);
                        }
                        continue;
                    }

                    if (index.modules.find(branch.target) == index.modules.end()) {
                        continue;
                    }

                    reverse[branch.target].insert(title);
                }
            }

            while (!queue.empty()) {
                auto current = std::move(queue.front());
                queue.pop_front();

                const auto found = reverse.find(current);
                if (found == reverse.end()) {
                    continue;
                }

                for (const auto& source : found->second) {
                    if (reaches.insert(source).second) {
                        queue.push_back(source);
                    }
                }
            }

            return reaches;
        }

    } // namespace

    std::vector<ProjectDiagnostic> validate(
        const LoadedProject& project,
        const ProjectIndex& index)
    {
        std::vector<ProjectDiagnostic> out;

        if (index.modules.empty()) {
            out.push_back(ProjectDiagnostic{
                project.wstory_path.empty() ? project.folder : project.wstory_path,
                diagnostics::Diagnostic{
                    std::string(diagnostics::DiagnosticCode::EmptyProject),
                    diagnostics::Severity::Error,
                    u"项目为空，没有可演示的模块。",
                    source::TextSpan{0, 0},
                    std::nullopt,
                },
            });
            return out;
        }

        // 先检查目标存在性；可达性分析只依赖语法树中的出口边。
        const auto entries = resolve_entries(project);
        const auto syntax_by_title = build_syntax_map(project, index);

        std::vector<std::u16string> entry_points;
        if (entries.default_entry) {
            entry_points.push_back(*entries.default_entry);
        }
        entry_points.insert(
            entry_points.end(),
            entries.candidates.begin(),
            entries.candidates.end());

        validate_targets(project, index, out);

        if (!entry_points.empty()) {
            const auto reachable =
                reachable_from(entry_points, syntax_by_title, index);
            for (const auto& [title, module] : index.modules) {
                if (reachable.find(title) == reachable.end()) {
                    std::u16string message = u"模块从入口不可达：";
                    message += title;
                    out.push_back(ProjectDiagnostic{
                        module.file_path,
                        diagnostics::Diagnostic{
                            std::string(diagnostics::DiagnosticCode::UnreachableModule),
                            diagnostics::Severity::Warning,
                            std::move(message),
                            module.title_span,
                            std::nullopt,
                        },
                    });
                }
            }
        }

        // 可达性和“能否到 END”分别给 warning；不阻断编译。
        const auto reaches_end = can_reach_end(syntax_by_title, index);
        for (const auto& [title, module] : index.modules) {
            if (reaches_end.find(title) == reaches_end.end()) {
                std::u16string message = u"模块无法到达 END：";
                message += title;
                out.push_back(ProjectDiagnostic{
                    module.file_path,
                    diagnostics::Diagnostic{
                        std::string(diagnostics::DiagnosticCode::PathCannotReachEnd),
                        diagnostics::Severity::Warning,
                        std::move(message),
                        module.title_span,
                        std::nullopt,
                    },
                });
            }
        }

        return out;
    }

} // namespace weftline::projects
