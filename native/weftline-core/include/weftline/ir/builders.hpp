#pragma once

#include <optional>
#include <string>

#include "weftline/ir/project_model.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"

namespace weftline::ir {

    /**
     * @brief 语义 IR 的构造入口。
     *
     * 数据流为 ProjectModel → StoryGraph；图推导、播放载荷和序列化分别在
     * graph / runtime / serialization 模块。
     */

    /**
     * @brief 从已加载项目和项目索引构建源级语义模型。
     *
     * 项目名、概要和 @doc 来自 .wstory；章节/模块顺序和声明顺序来自 .weft。
     */
    [[nodiscard]] ProjectModel build_project_model(
        const projects::LoadedProject& project,
        const projects::ProjectIndex& index);

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
