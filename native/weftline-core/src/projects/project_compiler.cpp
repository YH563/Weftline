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

        [[nodiscard]] bool has_error_diagnostics(
            const std::vector<ProjectDiagnostic>& diagnostics)
        {
            return std::any_of(
                diagnostics.begin(), diagnostics.end(),
                [](const ProjectDiagnostic& item) {
                    return item.diagnostic.severity == diagnostics::Severity::Error;
                });
        }

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
        auto project = load(snapshot, cache);
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

        auto validation = validate(project, binding.index);
        diagnostics.insert(
            diagnostics.end(), validation.begin(), validation.end());

        auto model = ir::build_project_model(project, binding.index);
        const bool errors = has_error_diagnostics(diagnostics);

        std::optional<std::shared_ptr<const ir::StoryGraph>> graph;
        std::optional<std::shared_ptr<const ir::GraphAnalysis>> analysis;
        std::optional<std::shared_ptr<const ir::GraphProjection>> projection;

        if (!errors || options.allow_partial) {
            auto* cached = cache != nullptr ? &cache->graph_cache() : nullptr;
            const bool can_reuse = cached != nullptr
                && cached->graph != nullptr
                && cached->analysis != nullptr
                && same_files(cached->files, project.weft_files);

            if (can_reuse) {
                graph = cached->graph;
                analysis = cached->analysis;

                if (options.build_projection) {
                    if (cached->has_projection && cached->projection != nullptr) {
                        projection = cached->projection;
                    } else {
                        auto built = std::make_shared<ir::GraphProjection>(
                            ir::build_graph_projection(
                                model, *cached->graph, *cached->analysis));
                        projection = built;
                        cached->projection = built;
                        cached->has_projection = true;
                    }
                }
            } else {
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
