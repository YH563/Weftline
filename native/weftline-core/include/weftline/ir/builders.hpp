#pragma once

#include <optional>
#include <string>

#include "weftline/ir/graph_analysis.hpp"
#include "weftline/ir/graph_projection.hpp"
#include "weftline/ir/project_model.hpp"
#include "weftline/ir/runtime_projection.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"

namespace weftline::ir {

    /**
     * @brief IR 构造管线的全部入口。
     *
     * 数据流固定为 ProjectModel → StoryGraph → GraphAnalysis → GraphProjection，
     * RuntimeProjection 由 ProjectModel + StoryGraph 得到。声明集中在本文件，
     * 实现仍按阶段分在各自的 *_builder.cpp：这样数据头保持纯数据，
     * 不会把 projects 层类型带给每个 IR 消费者。
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

    /**
     * @brief 从 StoryGraph 构建分析结果：邻接表、可达性、引用索引和章节聚合边。
     */
    [[nodiscard]] GraphAnalysis build_graph_analysis(
        const ProjectModel& model,
        const StoryGraph& graph);

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
