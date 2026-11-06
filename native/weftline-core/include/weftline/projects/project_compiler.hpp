#pragma once

#include <optional>
#include <vector>

#include "weftline/ir/graph_analysis.hpp"
#include "weftline/ir/graph_projection.hpp"
#include "weftline/ir/project_model.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/projects/project_snapshot.hpp"

namespace weftline::projects {

    class CompilationCache;

    /**
     * @brief 项目编译选项；实时预览使用 allow_partial + build_projection。
     */
    struct CompilationOptions
    {
        bool allow_partial{false};
        bool build_projection{false};
    };

    /**
     * @brief 一次项目编译的结果。
     */
    struct ProjectCompilationResult
    {
        LoadedProject project;
        ProjectIndex index;
        ir::ProjectModel model;
        std::vector<ProjectDiagnostic> diagnostics;
        std::optional<ir::StoryGraph> graph;
        std::optional<ir::GraphAnalysis> analysis;
        std::optional<ir::GraphProjection> projection;

        /// @brief 是否存在 error 级别的诊断。
        [[nodiscard]] bool has_errors() const;
    };

    /**
     * @brief 项目级编译入口：加载、绑定、校验、模型、图和投影。
     *
     * 有 error 且 allow_partial=false 时不产出 StoryGraph。
     * cache 非空时，文本未变化的文件复用已解析 AST。
     */
    [[nodiscard]] ProjectCompilationResult compile(
        const ProjectSnapshot& snapshot,
        const CompilationOptions& options = {},
        CompilationCache* cache = nullptr);

} // namespace weftline::projects
