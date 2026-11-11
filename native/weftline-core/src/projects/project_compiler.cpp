#include "weftline/projects/project_compiler.hpp"

#include <algorithm>
#include <memory>
#include <optional>
#include <utility>
#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/ir/graph_analysis_builder.hpp"
#include "weftline/ir/graph_projection_builder.hpp"
#include "weftline/ir/project_model_builder.hpp"
#include "weftline/ir/story_graph_builder.hpp"
#include "weftline/projects/compilation_cache.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/validator.hpp"

namespace weftline::projects {
    namespace {

        // 只有 error 会阻止产出正式图；warning 不影响编译。
        [[nodiscard]] bool has_error_diagnostics(
            const std::vector<ProjectDiagnostic>& diagnostics)
        {
            return std::any_of(
                diagnostics.begin(), diagnostics.end(),
                [](const ProjectDiagnostic& item) {
                    return item.diagnostic.severity == diagnostics::Severity::Error;
                });
        }

        // 图缓存 key：路径一致且 AST 指针一致才算同一结构。
        // AST 指针来自解析缓存，文本未变时保持不变。
        [[nodiscard]] bool same_files(
            const std::vector<ProjectFile>& left,
            const std::vector<ProjectFile>& right)
        {
            if (left.size() != right.size()) {
                return false;
            }

            for (std::size_t index = 0; index < left.size(); ++index) {
                if (left[index].file_path != right[index].file_path
                    || left[index].ast.get() != right[index].ast.get()) {
                    return false;
                }
            }

            return true;
        }

    } // namespace

    bool ProjectCompilationResult::has_errors() const
    {
        return has_error_diagnostics(diagnostics);
    }

    ProjectCompilationResult compile(
        const ProjectSnapshot& snapshot,
        const CompilationOptions& options,
        CompilationCache* cache)
    {
        // 阶段 1：加载并把未变化文件命中解析缓存。
        auto project = load(snapshot, cache);

        // 阶段 2：跨文件声明与模块引用；绑定缓存按符号签名选择性复用。
        auto binding = weftline::projects::bind(
            project.weft_files,
            project.wstory ? &*project.wstory : nullptr,
            project.wstory_path,
            cache != nullptr ? &cache->binding_cache() : nullptr);

        std::vector<ProjectDiagnostic> diagnostics;
        diagnostics.reserve(
            project.diagnostics.size()
            + binding.diagnostics.size());
        diagnostics.insert(
            diagnostics.end(),
            project.diagnostics.begin(),
            project.diagnostics.end());
        diagnostics.insert(
            diagnostics.end(),
            binding.diagnostics.begin(),
            binding.diagnostics.end());

        // 阶段 3：项目级校验（入口、目标、可达性等）。
        auto validation = validate(project, binding.index);
        diagnostics.insert(
            diagnostics.end(), validation.begin(), validation.end());

        // 阶段 4：构建源级模型；图阶段只依赖这里的章节/声明和 AST。
        auto model = ir::build_project_model(project, binding.index);
        const bool errors = has_error_diagnostics(diagnostics);

        std::optional<std::shared_ptr<const ir::StoryGraph>> graph;
        std::optional<std::shared_ptr<const ir::GraphAnalysis>> analysis;
        std::optional<std::shared_ptr<const ir::GraphProjection>> projection;

        // 阶段 5：图/分析/投影。strict 模式有 error 时不产出正式图。
        if (!errors || options.allow_partial) {
            auto* cached = cache != nullptr ? &cache->graph_cache() : nullptr;
            const bool can_reuse = cached != nullptr
                && cached->graph != nullptr
                && cached->analysis != nullptr
                && same_files(cached->files, project.weft_files);

            if (can_reuse) {
                // .weft AST 集合未变：图和分析可以整块复用（例如只改 .wstory 描述）。
                graph = cached->graph;
                analysis = cached->analysis;

                if (options.build_projection) {
                    if (cached->has_projection && cached->projection != nullptr) {
                        projection = cached->projection;
                    } else {
                        // 缓存里只有图，没有投影：只补建投影，不重算图和分析。
                        auto built = std::make_shared<ir::GraphProjection>(
                            ir::build_graph_projection(
                                model, *cached->graph, *cached->analysis));
                        projection = built;
                        cached->projection = built;
                        cached->has_projection = true;
                    }
                }
            } else {
                // 结构变化：重建图和分析；投影按请求决定是否构建。
                auto built_graph = std::make_shared<ir::StoryGraph>(
                    ir::build_story_graph(project, model));
                auto built_analysis = std::make_shared<ir::GraphAnalysis>(
                    ir::build_graph_analysis(model, *built_graph));

                graph = built_graph;
                analysis = built_analysis;

                if (options.build_projection) {
                    auto built_projection = std::make_shared<ir::GraphProjection>(
                        ir::build_graph_projection(
                            model, *built_graph, *built_analysis));
                    projection = built_projection;

                    if (cached != nullptr) {
                        cached->projection = built_projection;
                        cached->has_projection = true;
                    }
                } else if (cached != nullptr) {
                    cached->projection.reset();
                    cached->has_projection = false;
                }

                // 更新缓存 key 和结果，供下一次编译复用。
                if (cached != nullptr) {
                    cached->files = project.weft_files;
                    cached->graph = built_graph;
                    cached->analysis = built_analysis;
                }
            }
        }

        return ProjectCompilationResult{
            std::move(project),
            std::move(binding.index),
            std::move(model),
            std::move(diagnostics),
            std::move(graph),
            std::move(analysis),
            std::move(projection),
        };
    }

} // namespace weftline::projects
