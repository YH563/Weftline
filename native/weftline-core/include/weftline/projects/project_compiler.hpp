#pragma once

#include <memory>
#include <optional>
#include <vector>

#include "weftline/ir/graph_analysis.hpp"
#include "weftline/ir/graph_projection.hpp"
#include "weftline/ir/project_model.hpp"
#include "weftline/ir/runtime_projection.hpp"
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
    };

    /**
     * @brief 一次项目编译的结果；图/分析/投影可能由缓存复用，runtime 按需构建。
     */
    struct ProjectCompilationResult
    {
        LoadedProject project;
        ProjectIndex index;
        ir::ProjectModel model;
        std::vector<ProjectDiagnostic> diagnostics;
        std::optional<std::shared_ptr<const ir::StoryGraph>> graph;
        std::optional<std::shared_ptr<const ir::GraphAnalysis>> analysis;
        std::optional<std::shared_ptr<const ir::GraphProjection>> projection;
        std::optional<std::shared_ptr<const ir::RuntimeProjection>> runtime;

        /// @brief 是否存在 error 级别的诊断。
        [[nodiscard]] bool has_errors() const;
    };

    /**
     * @brief 项目级编译入口：加载、绑定、校验、模型、图和投影。
     *
     * 有 error 且 allow_partial=false 时不产出 StoryGraph。
     * build_runtime 为 true 且无 error 时构建播放器载荷；有 error 时 runtime 为空，
     * 由调用方保留上一次可用载荷。
     * cache 非空时：文本未变化的文件复用 AST；符号签名不变时复用未变化文件的
     * 模块诊断；.weft AST 集合不变时复用图/分析/投影。
     */
    [[nodiscard]] ProjectCompilationResult compile(
        const ProjectSnapshot& snapshot,
        const CompilationOptions& options = {},
        CompilationCache* cache = nullptr);

} // namespace weftline::projects
