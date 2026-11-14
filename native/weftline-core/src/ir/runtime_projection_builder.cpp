#include "weftline/ir/runtime_projection_builder.hpp"

#include <optional>
#include <utility>
#include <vector>

namespace weftline::ir {
    namespace {

        // 播放器不需要源码位置，分支只保留执行语义。
        [[nodiscard]] RuntimeBranch to_runtime_branch(const IrExitBranch& branch)
        {
            return RuntimeBranch{
                branch.condition,
                branch.label,
                branch.target,
                branch.effects,
            };
        }

        [[nodiscard]] RuntimeExit to_runtime_exit(const IrExitBlock& exit)
        {
            std::vector<RuntimeBranch> branches;
            branches.reserve(exit.branches.size());

            for (const auto& branch : exit.branches) {
                branches.push_back(to_runtime_branch(branch));
            }

            return RuntimeExit{
                exit.mode,
                exit.character,
                std::move(branches),
            };
        }

        [[nodiscard]] RuntimeModule to_runtime_module(const IrModule& module)
        {
            std::optional<RuntimeExit> exit;
            if (module.exit) {
                exit = to_runtime_exit(*module.exit);
            }

            std::vector<RuntimeScene> scenes;
            scenes.reserve(module.scenes.size());
            for (const auto& marker : module.scenes) {
                scenes.push_back(RuntimeScene{
                    marker.name,
                    marker.content_index,
                });
            }

            return RuntimeModule{
                module.id,
                module.chapter_id,
                std::move(scenes),
                module.content,
                std::move(exit),
            };
        }

    } // namespace

    RuntimeProjection build_runtime_projection(
        const ProjectModel& model,
        const StoryGraph& graph)
    {
        RuntimeProjection runtime;
        runtime.schema_version = 1;
        runtime.project_name = model.project.name;
        runtime.entry = graph.entry;
        runtime.end = graph.end;

        runtime.chapters.reserve(model.chapters.size());
        for (const auto& chapter : model.chapters) {
            runtime.chapters.push_back(RuntimeChapter{chapter.id, chapter.title});
        }

        runtime.globals = graph.globals;

        runtime.modules.reserve(graph.modules.size());
        for (const auto& module : graph.modules) {
            runtime.modules.push_back(to_runtime_module(module));
        }

        return runtime;
    }

} // namespace weftline::ir
