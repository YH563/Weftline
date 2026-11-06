#include "weftline/ir/graph_analysis.hpp"

#include <cstddef>
#include <string>
#include <string_view>
#include <unordered_set>
#include <vector>

namespace weftline::ir {

    std::vector<std::vector<std::u16string>> GraphAnalysis::find_paths(
        std::u16string_view from,
        std::u16string_view to,
        std::size_t limit) const
    {
        std::vector<std::vector<std::u16string>> results;

        if (limit == 0 || from.empty()) {
            return results;
        }

        const auto start = adjacency.find(std::u16string(from));
        if (start == adjacency.end()) {
            return results;
        }

        std::vector<std::u16string> path;
        std::unordered_set<std::u16string> on_path;

        const auto visit = [&](const auto& self, const std::u16string& current) -> void {
            if (results.size() >= limit) {
                return;
            }

            path.push_back(current);
            on_path.insert(current);

            if (std::u16string_view(current) == to) {
                results.push_back(path);
            } else {
                const auto next = adjacency.find(current);
                if (next != adjacency.end()) {
                    for (const auto& target : next->second) {
                        if (on_path.find(target) == on_path.end()) {
                            self(self, target);
                        }
                    }
                }
            }

            on_path.erase(current);
            path.pop_back();
        };

        visit(visit, std::u16string(from));
        return results;
    }

} // namespace weftline::ir
