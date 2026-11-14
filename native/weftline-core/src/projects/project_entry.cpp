#include "weftline/projects/project_entry.hpp"

#include <string>
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
                if (!result.default_entry) {
                    result.default_entry = module.title;
                }
                modules.push_back(&module);
            }
        }

        std::unordered_set<std::u16string> incoming;
        for (const auto* module : modules) {
            if (!module->exit) {
                continue;
            }

            for (const auto& branch : module->exit->branches) {
                if (branch.target != u"END" && known.contains(branch.target)) {
                    incoming.insert(branch.target);
                }
            }
        }

        for (const auto* module : modules) {
            if (result.default_entry && module->title == *result.default_entry) {
                continue;
            }
            if (!incoming.contains(module->title)) {
                result.candidates.push_back(module->title);
            }
        }

        return result;
    }

} // namespace weftline::projects
