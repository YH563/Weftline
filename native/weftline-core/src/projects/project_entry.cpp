#include "weftline/projects/project_entry.hpp"

#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include "weftline/syntax/ast.hpp"

namespace weftline::projects {

    ProjectEntries resolve_entries(const LoadedProject& project)
    {
        ProjectEntries result;

        std::vector<const syntax::Module*> modules;
        std::unordered_set<std::u16string> known;

        for (const auto& file : project.weft_files) {
            for (const auto& module : file.ast->modules) {
                if (!known.insert(module.title).second) {
                    continue;
                }
                modules.push_back(&module);
            }
        }

        // 入度按“不同前驱模块数”计算；并行分支不重复计数。
        std::unordered_map<std::u16string, std::unordered_set<std::u16string>>
            predecessors;
        for (const auto* module : modules) {
            if (!module->exit) {
                continue;
            }

            for (const auto& branch : module->exit->branches) {
                if (branch.target != u"END" && known.contains(branch.target)) {
                    predecessors[branch.target].insert(module->title);
                }
            }
        }

        if (project.wstory && project.wstory->entry) {
            result.default_entry = *project.wstory->entry;
            result.is_explicit = true;
        } else if (!modules.empty()) {
            // 默认入口：全局入度最小的模块；同分保留源顺序中的第一个。
            std::size_t min_in_degree = 0;
            bool has_default = false;

            for (const auto* module : modules) {
                const auto found = predecessors.find(module->title);
                const auto in_degree =
                    found == predecessors.end() ? 0 : found->second.size();

                if (!has_default || in_degree < min_in_degree) {
                    min_in_degree = in_degree;
                    result.default_entry = module->title;
                    has_default = true;
                }
            }

            result.default_in_degree = min_in_degree;
        }

        // 候选入口仍表示零入度模块；它不改变默认入口的可达性语义。
        for (const auto* module : modules) {
            if (result.default_entry && module->title == *result.default_entry) {
                continue;
            }

            const auto found = predecessors.find(module->title);
            if (found == predecessors.end() || found->second.empty()) {
                result.candidates.push_back(module->title);
            }
        }

        return result;
    }

} // namespace weftline::projects
