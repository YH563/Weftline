#include "weftline/ir/graph_analysis.hpp"

#include <cstddef>
#include <string>
#include <string_view>
#include <unordered_set>
#include <vector>

namespace weftline::ir {

    // 枚举简单路径：on_path 防止环，limit 控制组合爆炸。
    // 当前图视图只用最短路径；这个接口留给后续“全部路径/结局”功能。
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

        // 递归回溯：进入节点时记录，回溯时撤销，保证同一路径不重复访问节点。
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
