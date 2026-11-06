#pragma once

#include <optional>
#include <string>

#include "weftline/ir/project_model.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"

namespace weftline::ir {

    /**
     * @brief 从项目 AST 和 ProjectModel 构建 StoryGraph。
     *
     * @param entry 入口模块；为空时使用第一个模块。
     */
    [[nodiscard]] StoryGraph build_story_graph(
        const projects::LoadedProject& project,
        const ProjectModel& model,
        std::optional<std::u16string> entry = std::nullopt);

    /**
     * @brief 先构建 ProjectModel，再构建 StoryGraph。
     */
    [[nodiscard]] StoryGraph build_story_graph(
        const projects::LoadedProject& project,
        const projects::ProjectIndex& index,
        std::optional<std::u16string> entry = std::nullopt);

} // namespace weftline::ir
