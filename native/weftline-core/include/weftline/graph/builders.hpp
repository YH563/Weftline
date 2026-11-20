#pragma once

#include "weftline/graph/graph_analysis.hpp"
#include "weftline/graph/graph_projection.hpp"
#include "weftline/ir/project_model.hpp"
#include "weftline/ir/story_graph.hpp"

namespace weftline::graph {

    /**
     * @brief 图推导入口：从 StoryGraph 构建分析结果和 UI 压缩投影。
     *
     * 数据流为 StoryGraph → GraphAnalysis → GraphProjection；两者都是派生数据，
     * 不写回 IR。
     */

    /**
     * @brief 从 StoryGraph 构建分析结果：邻接表、可达性、引用索引和章节聚合边。
     */
    [[nodiscard]] GraphAnalysis build_graph_analysis(
        const ir::ProjectModel& model,
        const ir::StoryGraph& graph);

    /**
     * @brief 从 StoryGraph + GraphAnalysis 构建压缩投影。
     */
    [[nodiscard]] GraphProjection build_graph_projection(
        const ir::ProjectModel& model,
        const ir::StoryGraph& graph,
        const GraphAnalysis& analysis);

    /**
     * @brief 先构建 GraphAnalysis，再构建投影。
     */
    [[nodiscard]] GraphProjection build_graph_projection(
        const ir::ProjectModel& model,
        const ir::StoryGraph& graph);

} // namespace weftline::graph
