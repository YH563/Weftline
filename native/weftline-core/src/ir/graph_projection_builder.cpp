#include "weftline/ir/graph_projection_builder.hpp"

#include <algorithm>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

#include "weftline/ir/graph_analysis_builder.hpp"
#include "weftline/source/text_utils.hpp"

namespace weftline::ir {
    namespace {

        [[nodiscard]] std::unordered_map<std::u16string, const IrModule*> by_id(
            const StoryGraph& graph)
        {
            std::unordered_map<std::u16string, const IrModule*> modules;
            for (const auto& module : graph.modules) {
                modules.emplace(module.id, &module);
            }
            return modules;
        }

        // 章节角标：入口、是否有跨章节入/出边、是否有到 END 的边。
        [[nodiscard]] GraphChapter build_chapter(
            std::u16string chapter_id,
            std::u16string title,
            std::vector<std::u16string> module_ids,
            const std::optional<std::u16string>& entry_module_id,
            const std::unordered_set<std::u16string>& entry_candidates,
            const std::unordered_set<std::u16string>& incoming,
            const std::unordered_set<std::u16string>& outgoing,
            const std::unordered_set<std::u16string>& end_edges)
        {
            auto flags = GraphChapterFlags::None;

            if (entry_module_id
                && std::find(
                       module_ids.begin(), module_ids.end(), *entry_module_id)
                    != module_ids.end()) {
                flags = flags | GraphChapterFlags::Entry;
            }
            if (std::any_of(
                    module_ids.begin(),
                    module_ids.end(),
                    [&](const std::u16string& module_id) {
                        return entry_candidates.find(module_id)
                            != entry_candidates.end();
                    })) {
                flags = flags | GraphChapterFlags::EntryCandidate;
            }
            if (incoming.find(chapter_id) != incoming.end()) {
                flags = flags | GraphChapterFlags::HasIncoming;
            }
            if (outgoing.find(chapter_id) != outgoing.end()) {
                flags = flags | GraphChapterFlags::HasOutgoing;
            }
            if (end_edges.find(chapter_id) != end_edges.end()) {
                flags = flags | GraphChapterFlags::HasEndEdge;
            }

            return GraphChapter{
                std::move(chapter_id),
                std::move(title),
                std::move(module_ids),
                flags,
            };
        }

        // 章节顺序以 ProjectModel 为准；没有声明章节的模块补一个同名虚拟章节。
        [[nodiscard]] std::vector<GraphChapter> build_chapters(
            const ProjectModel& model,
            const StoryGraph& graph,
            const GraphAnalysis& analysis,
            const std::unordered_set<std::u16string>& entry_candidates)
        {
            std::unordered_map<std::u16string, std::vector<const IrModule*>>
                modules_by_chapter;
            std::vector<std::u16string> chapter_order;

            for (const auto& module : graph.modules) {
                auto& modules = modules_by_chapter[module.chapter_id];
                if (modules.empty()) {
                    chapter_order.push_back(module.chapter_id);
                }
                modules.push_back(&module);
            }

            std::unordered_set<std::u16string> incoming;
            std::unordered_set<std::u16string> outgoing;
            std::unordered_set<std::u16string> end_edges;

            for (const auto& edge : analysis.chapter_edges) {
                if (edge.to_chapter == graph.end) {
                    end_edges.insert(edge.from_chapter);
                } else {
                    outgoing.insert(edge.from_chapter);
                    incoming.insert(edge.to_chapter);
                }
            }

            std::vector<GraphChapter> chapters;
            std::unordered_set<std::u16string> covered;

            for (const auto& chapter : model.chapters) {
                std::vector<std::u16string> module_ids;
                const auto found = modules_by_chapter.find(chapter.id);
                if (found != modules_by_chapter.end()) {
                    for (const auto* module : found->second) {
                        module_ids.push_back(module->id);
                    }
                }

                chapters.push_back(build_chapter(
                    chapter.id,
                    chapter.title,
                    std::move(module_ids),
                    graph.entry,
                    entry_candidates,
                    incoming,
                    outgoing,
                    end_edges));
                covered.insert(chapter.id);
            }

            for (const auto& chapter_id : chapter_order) {
                if (covered.find(chapter_id) != covered.end()) {
                    continue;
                }

                std::vector<std::u16string> module_ids;
                for (const auto* module : modules_by_chapter[chapter_id]) {
                    module_ids.push_back(module->id);
                }

                chapters.push_back(build_chapter(
                    chapter_id,
                    chapter_id,
                    std::move(module_ids),
                    graph.entry,
                    entry_candidates,
                    incoming,
                    outgoing,
                    end_edges));
            }

            return chapters;
        }

        [[nodiscard]] std::u16string segment_text(
            const std::vector<IrTextSegment>& segments)
        {
            std::u16string text;
            for (const auto& segment : segments) {
                if (segment.kind == TextSegmentKind::Text) {
                    text += segment.text;
                } else {
                    text += segment.name;
                }
            }
            return text;
        }

        [[nodiscard]] std::optional<std::u16string> first_line(
            const IrContentBlock& content)
        {
            std::u16string text;

            switch (content.kind) {
                case ContentKind::Narration:
                case ContentKind::Dialogue:
                    text = segment_text(content.segments);
                    break;
                case ContentKind::Figure:
                    text = content.path ? *content.path : std::u16string{};
                    break;
            }

            if (source::trim(text).empty()) {
                return std::nullopt;
            }

            const auto newline = text.find(u'\n');
            if (newline != std::u16string::npos) {
                text.resize(newline);
            }

            const auto trimmed = source::trim(text);
            if (trimmed.empty()) {
                return std::nullopt;
            }

            return std::u16string(trimmed);
        }

        // 悬停摘要：首个非空内容块的首行、对话数、选项数。
        [[nodiscard]] GraphModuleSummary build_summary(const IrModule& module)
        {
            std::optional<std::u16string> line;
            for (const auto& content : module.content) {
                line = first_line(content);
                if (line) {
                    break;
                }
            }

            std::int32_t dialogue_count = 0;
            for (const auto& content : module.content) {
                if (content.kind == ContentKind::Dialogue) {
                    ++dialogue_count;
                }
            }

            std::int32_t choice_count = 0;
            if (module.exit && module.exit->mode == ExitMode::Choice) {
                choice_count = static_cast<std::int32_t>(module.exit->branches.size());
            }

            return GraphModuleSummary{
                std::move(line),
                dialogue_count,
                choice_count,
            };
        }

        // 模块角标：默认入口、候选入口、有 END 出口、从入口不可达、无法到达 END。
        [[nodiscard]] GraphModuleFlags build_module_flags(
            const IrModule& module,
            const StoryGraph& graph,
            const GraphAnalysis& analysis,
            const std::unordered_set<std::u16string>& entry_candidates)
        {
            auto flags = GraphModuleFlags::None;

            if (graph.entry && module.id == *graph.entry) {
                flags = flags | GraphModuleFlags::Entry;
            } else if (entry_candidates.find(module.id) != entry_candidates.end()) {
                flags = flags | GraphModuleFlags::EntryCandidate;
            }

            if (module.exit) {
                const auto has_end = std::any_of(
                    module.exit->branches.begin(),
                    module.exit->branches.end(),
                    [&](const IrExitBranch& branch) {
                        return branch.target == graph.end;
                    });
                if (has_end) {
                    flags = flags | GraphModuleFlags::TargetsEnd;
                }
            }

            if (analysis.reachable.find(module.id) == analysis.reachable.end()) {
                flags = flags | GraphModuleFlags::Unreachable;
            }

            if (analysis.can_reach_end.find(module.id)
                == analysis.can_reach_end.end()) {
                flags = flags | GraphModuleFlags::CannotReachEnd;
            }

            return flags;
        }

        // 场景列表按源顺序去重，避免同一模块重复显示同一个场景。
        [[nodiscard]] std::vector<std::u16string> build_scenes(
            const IrModule& module)
        {
            std::vector<std::u16string> scenes;
            std::unordered_set<std::u16string> seen;

            for (const auto& marker : module.scenes) {
                if (seen.insert(marker.name).second) {
                    scenes.push_back(marker.name);
                }
            }

            return scenes;
        }

        // 模块节点只带图视图需要的元数据，不复制完整正文。
        [[nodiscard]] std::vector<GraphModuleNode> build_modules(
            const StoryGraph& graph,
            const GraphAnalysis& analysis,
            const std::unordered_set<std::u16string>& entry_candidates)
        {
            std::vector<GraphModuleNode> modules;
            modules.reserve(graph.modules.size());

            for (const auto& module : graph.modules) {
                modules.push_back(GraphModuleNode{
                    module.id,
                    module.chapter_id,
                    build_scenes(module),
                    module.exit
                        ? static_cast<std::int32_t>(module.exit->branches.size())
                        : 0,
                    build_module_flags(
                        module, graph, analysis, entry_candidates),
                    build_summary(module),
                    module.source,
                });
            }

            return modules;
        }

        // 每个出口分支生成一条边；external 标记目标是否在另一章节。
        [[nodiscard]] std::vector<GraphModuleEdge> build_module_edges(
            const StoryGraph& graph,
            const std::unordered_map<std::u16string, const IrModule*>& modules_by_id)
        {
            std::vector<GraphModuleEdge> edges;

            for (const auto& module : graph.modules) {
                if (!module.exit) {
                    continue;
                }

                for (const auto& branch : module.exit->branches) {
                    bool external = false;

                    if (branch.target != graph.end) {
                        const auto target = modules_by_id.find(branch.target);
                        if (target != modules_by_id.end()) {
                            external = target->second->chapter_id != module.chapter_id;
                        }
                    }

                    edges.push_back(GraphModuleEdge{
                        module.id,
                        branch.target,
                        module.exit->mode,
                        branch.label,
                        branch.condition,
                        external,
                        branch.source,
                    });
                }
            }

            return edges;
        }

    } // namespace

    // 图视图投影：章节容器、模块节点/边、章节聚合边和筛选引用索引。
    GraphProjection build_graph_projection(
        const ProjectModel& model,
        const StoryGraph& graph,
        const GraphAnalysis& analysis)
    {
        const auto modules_by_id = by_id(graph);
        const std::unordered_set<std::u16string> entry_candidates(
            graph.entry_candidates.begin(), graph.entry_candidates.end());

        return GraphProjection{
            graph.entry,
            graph.entry_candidates,
            build_chapters(model, graph, analysis, entry_candidates),
            build_modules(graph, analysis, entry_candidates),
            build_module_edges(graph, modules_by_id),
            analysis.chapter_edges,
            analysis.references,
        };
    }

    GraphProjection build_graph_projection(
        const ProjectModel& model,
        const StoryGraph& graph)
    {
        const auto analysis = build_graph_analysis(model, graph);
        return build_graph_projection(model, graph, analysis);
    }

} // namespace weftline::ir
