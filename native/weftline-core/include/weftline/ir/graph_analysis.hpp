#pragma once

#include <cstddef>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include "weftline/ir/story_graph.hpp"
#include "weftline/source/location.hpp"

namespace weftline::ir {

    /**
     * @brief 按符号种类归类的引用索引。
     *
     * 值是有序、去重的模块 ID 列表。
     */
    struct ReferenceIndex
    {
        std::unordered_map<std::u16string, std::vector<std::u16string>> characters;
        std::unordered_map<std::u16string, std::vector<std::u16string>> items;
        std::unordered_map<std::u16string, std::vector<std::u16string>> scenes;
        std::unordered_map<std::u16string, std::vector<std::u16string>> variables;
    };

    /**
     * @brief 聚合章节边下的一条底层模块分支。
     */
    struct ChapterEdgeDetail
    {
        std::u16string from_module;
        std::u16string to_module;
        ExitMode mode{ExitMode::Auto};
        std::optional<std::u16string> label;
        std::optional<IrBoolExpr> condition;
        source::SourceLocation source;
    };

    /**
     * @brief 一条聚合后的章节边；count 等于底层模块分支条数。
     */
    struct ChapterEdge
    {
        std::u16string from_chapter;
        std::u16string to_chapter;
        std::vector<ChapterEdgeDetail> details;

        [[nodiscard]] std::size_t count() const noexcept
        {
            return details.size();
        }
    };

    /**
     * @brief 从 StoryGraph 派生的分析结果。
     *
     * 不进入 IR，也不写回 StoryGraph；图视图、筛选和路径高亮共用。
     */
    struct GraphAnalysis
    {
        std::unordered_set<std::u16string> reachable;
        std::unordered_set<std::u16string> can_reach_end;
        ReferenceIndex references;
        std::unordered_map<std::u16string, std::vector<std::u16string>> adjacency;
        std::unordered_map<std::u16string, std::vector<std::u16string>> reverse_adjacency;
        std::vector<ChapterEdge> chapter_edges;

        /**
         * @brief 枚举从 from 到 to 的简单路径，最多返回 limit 条。
         *
         * to 可以是模块 ID，也可以是 StoryGraph::end（通常是 "END"）。
         */
        [[nodiscard]] std::vector<std::vector<std::u16string>> find_paths(
            std::u16string_view from,
            std::u16string_view to,
            std::size_t limit = 50) const;
    };

} // namespace weftline::ir
