#pragma once

#include "weftline/ir/graph_analysis.hpp"
#include "weftline/ir/graph_projection.hpp"
#include "weftline/ir/project_model.hpp"
#include "weftline/ir/story_graph.hpp"

namespace weftline::ir {

    /**
     * @brief 从 StoryGraph + GraphAnalysis 构建压缩投影。
     */
    [[nodiscard]] GraphProjection build_graph_projection(
        const ProjectModel& model,
        const StoryGraph& graph,
        const GraphAnalysis& analysis);

    /**
     * @brief 先构建 GraphAnalysis，再构建投影。
     */
    [[nodiscard]] GraphProjection build_graph_projection(
        const ProjectModel& model,
        const StoryGraph& graph);

} // namespace weftline::ir
