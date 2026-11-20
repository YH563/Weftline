#pragma once

#include "weftline/ir/project_model.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/runtime/runtime_projection.hpp"

namespace weftline::runtime {

    /**
     * @brief 从 ProjectModel 和 StoryGraph 构建播放器载荷。
     *
     * 章节标题来自 ProjectModel；模块、出口、全局变量和入口来自 StoryGraph。
     * 输出保持源顺序，并丢弃所有 SourceLocation。
     */
    [[nodiscard]] RuntimeProjection build_runtime_projection(
        const ir::ProjectModel& model,
        const ir::StoryGraph& graph);

} // namespace weftline::runtime
