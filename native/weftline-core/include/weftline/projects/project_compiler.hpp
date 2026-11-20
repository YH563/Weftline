#pragma once

#include <memory>
#include <optional>
#include <string>
#include <vector>

#include "weftline/graph/graph_analysis.hpp"
#include "weftline/graph/graph_projection.hpp"
#include "weftline/ir/project_model.hpp"
#include "weftline/runtime/runtime_projection.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/projects/project_snapshot.hpp"

namespace weftline::projects {

    class CompilationCache;

    /**
     * @brief 项目编译选项；实时预览使用 allow_partial + build_projection + build_runtime。
     */
    struct CompilationOptions
    {
        bool allow_partial{false};
        bool build_projection{false};
        bool build_runtime{false};
        bool build_wstory{false};
    };

    /**
     * @brief 一次项目编译的结果；图/分析/投影可能由缓存复用，runtime 和 wstory 按需构建。
     */
    struct ProjectCompilationResult
    {
        LoadedProject project;
        ProjectIndex index;
        ir::ProjectModel model;
        std::vector<ProjectDiagnostic> diagnostics;
        std::optional<std::shared_ptr<const ir::StoryGraph>> graph;
        std::optional<std::shared_ptr<const graph::GraphAnalysis>> analysis;
        std::optional<std::shared_ptr<const graph::GraphProjection>> projection;
        std::optional<std::shared_ptr<const runtime::RuntimeProjection>> runtime;
        std::optional<std::u16string> wstory;

        /// @brief 是否存在 error 级别的诊断。
        [[nodiscard]] bool has_errors() const;
    };

    /**
     * @brief 项目级编译入口：加载、绑定、校验、模型、图和投影。
     *
     * 有 error 且 allow_partial=false 时不产出 StoryGraph。
     * build_runtime 为 true 且无 error 时构建播放器载荷；有 error 时 runtime 为空，
     * 由调用方保留上一次可用载荷。
     * build_wstory 为 true 时按当前 .weft AST 和现有 .wstory 描述生成项目总览文本；
     * 有 error 时仍生成，调用方自行决定是否落盘。
     * cache 非空时：文本未变化的文件复用 AST；符号签名不变时复用未变化文件的
     * 模块诊断；.weft AST 集合不变时复用图/分析/投影。
     */
    [[nodiscard]] ProjectCompilationResult compile(
        const ProjectSnapshot& snapshot,
        const CompilationOptions& options = {},
        CompilationCache* cache = nullptr);

} // namespace weftline::projects
