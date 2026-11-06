#include "weftline/projects/project_compiler.hpp"

#include <algorithm>
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
            project.wstory_path);

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

        std::optional<ir::StoryGraph> graph;
        std::optional<ir::GraphAnalysis> analysis;
        std::optional<ir::GraphProjection> projection;

        if (!errors || options.allow_partial) {
            graph = ir::build_story_graph(project, model);
            analysis = ir::build_graph_analysis(model, *graph);

            if (options.build_projection) {
                projection = ir::build_graph_projection(model, *graph, *analysis);
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
