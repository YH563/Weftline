#pragma once

#include "weftline/ir/graph_analysis.hpp"
#include "weftline/ir/project_model.hpp"
#include "weftline/ir/story_graph.hpp"

namespace weftline::ir {

    /**
     * @brief 从 StoryGraph 构建分析结果：邻接表、可达性、引用索引和章节聚合边。
     */
    [[nodiscard]] GraphAnalysis build_graph_analysis(
        const ProjectModel& model,
        const StoryGraph& graph);

} // namespace weftline::ir
