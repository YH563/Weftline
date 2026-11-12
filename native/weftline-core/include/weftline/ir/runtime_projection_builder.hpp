#pragma once

#include "weftline/ir/project_model.hpp"
#include "weftline/ir/runtime_projection.hpp"
#include "weftline/ir/story_graph.hpp"

namespace weftline::ir {

    /**
     * @brief 从 ProjectModel 和 StoryGraph 构建播放器载荷。
     *
     * 章节标题来自 ProjectModel；模块、出口、全局变量和入口来自 StoryGraph。
     * 输出保持源顺序，并丢弃所有 SourceLocation。
     */
    [[nodiscard]] RuntimeProjection build_runtime_projection(
        const ProjectModel& model,
        const StoryGraph& graph);

} // namespace weftline::ir
