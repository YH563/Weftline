#include "weftline/ir/graph_analysis_builder.hpp"

#include <deque>
#include <map>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

namespace weftline::ir {
    namespace {

        using StringList = std::vector<std::u16string>;
        using Index = std::unordered_map<std::u16string, StringList>;

        [[nodiscard]] std::unordered_map<std::u16string, const IrModule*> by_id(
            const StoryGraph& graph)
        {
            std::unordered_map<std::u16string, const IrModule*> modules;
            for (const auto& module : graph.modules) {
                modules.emplace(module.id, &module);
            }
            return modules;
        }

        [[nodiscard]] std::unordered_map<std::u16string, StringList> build_adjacency(
            const StoryGraph& graph)
        {
            std::unordered_map<std::u16string, StringList> adjacency;

            for (const auto& module : graph.modules) {
                StringList targets;
                std::unordered_set<std::u16string> seen;

                if (module.exit) {
                    for (const auto& branch : module.exit->branches) {
                        if (seen.insert(branch.target).second) {
                            targets.push_back(branch.target);
                        }
                    }
                }

                adjacency.emplace(module.id, std::move(targets));
            }

            return adjacency;
        }

        [[nodiscard]] std::unordered_map<std::u16string, StringList> build_reverse_adjacency(
            const std::unordered_map<std::u16string, StringList>& adjacency)
        {
            std::unordered_map<std::u16string, StringList> reverse;

            for (const auto& [from, targets] : adjacency) {
                for (const auto& target : targets) {
                    auto& sources = reverse[target];
                    if (sources.empty() || sources.back() != from) {
                        sources.push_back(from);
                    }
                }
            }

            return reverse;
        }

        [[nodiscard]] std::unordered_set<std::u16string> compute_reachable(
            const StoryGraph& graph,
            const std::unordered_map<std::u16string, const IrModule*>& modules_by_id,
            const std::unordered_map<std::u16string, StringList>& adjacency)
        {
            std::unordered_set<std::u16string> reachable;

            if (!graph.entry || modules_by_id.find(*graph.entry) == modules_by_id.end()) {
                return reachable;
            }

            std::deque<std::u16string> queue;
            queue.push_back(*graph.entry);

            while (!queue.empty()) {
                auto current = std::move(queue.front());
                queue.pop_front();

                if (!reachable.insert(current).second) {
                    continue;
                }

                const auto next = adjacency.find(current);
                if (next == adjacency.end()) {
                    continue;
                }

                for (const auto& target : next->second) {
                    if (target != graph.end
                        && modules_by_id.find(target) != modules_by_id.end()
                        && reachable.find(target) == reachable.end()) {
                        queue.push_back(target);
                    }
                }
            }

            return reachable;
        }

        [[nodiscard]] std::unordered_set<std::u16string> compute_can_reach_end(
            const StoryGraph& graph,
            const std::unordered_map<std::u16string, StringList>& reverse_adjacency)
        {
            std::unordered_set<std::u16string> can_reach_end;

            const auto direct = reverse_adjacency.find(graph.end);
            if (direct == reverse_adjacency.end()) {
                return can_reach_end;
            }

            std::deque<std::u16string> queue;
            for (const auto& source : direct->second) {
                if (can_reach_end.insert(source).second) {
                    queue.push_back(source);
                }
            }

            while (!queue.empty()) {
                auto current = std::move(queue.front());
                queue.pop_front();

                const auto sources = reverse_adjacency.find(current);
                if (sources == reverse_adjacency.end()) {
                    continue;
                }

                for (const auto& source : sources->second) {
                    if (can_reach_end.insert(source).second) {
                        queue.push_back(source);
                    }
                }
            }

            return can_reach_end;
        }

        void add_reference(
            Index& index,
            const std::optional<std::u16string>& name,
            const std::u16string& module_id)
        {
            if (!name || name->empty()) {
                return;
            }

            auto& modules = index[*name];
            if (modules.empty() || modules.back() != module_id) {
                modules.push_back(module_id);
            }
        }

        template <typename Add>
        void add_condition_variables(const IrBoolExpr& expression, Add add)
        {
            switch (expression.kind) {
                case BoolExprKind::Ref:
                    if (!expression.name.empty()) {
                        add(expression.name);
                    }
                    break;
                case BoolExprKind::Not:
                    if (expression.operand) {
                        add_condition_variables(*expression.operand, add);
                    }
                    break;
                case BoolExprKind::And:
                case BoolExprKind::Or:
                    if (expression.left) {
                        add_condition_variables(*expression.left, add);
                    }
                    if (expression.right) {
                        add_condition_variables(*expression.right, add);
                    }
                    break;
                case BoolExprKind::Literal:
                    break;
            }
        }

        void add_segment_references(
            const std::vector<IrTextSegment>& segments,
            const std::u16string& module_id,
            Index& characters,
            Index& items,
            const std::unordered_set<std::u16string>& character_names,
            const std::unordered_set<std::u16string>& item_names)
        {
            for (const auto& segment : segments) {
                if (segment.kind != TextSegmentKind::Ref || segment.name.empty()) {
                    continue;
                }

                if (character_names.find(segment.name) != character_names.end()) {
                    add_reference(characters, segment.name, module_id);
                } else if (item_names.find(segment.name) != item_names.end()) {
                    add_reference(items, segment.name, module_id);
                }
            }
        }

        [[nodiscard]] ReferenceIndex build_references(
            const ProjectModel& model,
            const StoryGraph& graph)
        {
            Index characters;
            Index items;
            Index scenes;
            Index variables;

            std::unordered_set<std::u16string> character_names;
            for (const auto& character : model.symbols.characters) {
                character_names.insert(character.name);
            }

            std::unordered_set<std::u16string> item_names;
            for (const auto& item : model.symbols.items) {
                item_names.insert(item.name);
            }

            for (const auto& module : graph.modules) {
                const auto add_variable = [&](const std::u16string& name) {
                    add_reference(variables, name, module.id);
                };

                add_reference(scenes, module.scene, module.id);

                for (const auto& content : module.content) {
                    if (content.kind == ContentKind::Dialogue) {
                        add_reference(characters, content.speaker, module.id);
                        add_segment_references(
                            content.segments, module.id, characters, items,
                            character_names, item_names);
                    } else if (content.kind == ContentKind::Narration) {
                        add_segment_references(
                            content.segments, module.id, characters, items,
                            character_names, item_names);
                    }
                }

                if (!module.exit) {
                    continue;
                }

                add_reference(characters, module.exit->character, module.id);

                for (const auto& branch : module.exit->branches) {
                    if (branch.condition) {
                        add_condition_variables(*branch.condition, add_variable);
                    }

                    for (const auto& effect : branch.effects) {
                        add_reference(variables, effect.name, module.id);
                    }
                }
            }

            return ReferenceIndex{
                std::move(characters),
                std::move(items),
                std::move(scenes),
                std::move(variables),
            };
        }

        [[nodiscard]] std::vector<ChapterEdge> build_chapter_edges(
            const StoryGraph& graph,
            const std::unordered_map<std::u16string, const IrModule*>& modules_by_id)
        {
            std::vector<ChapterEdge> edges;
            std::map<std::pair<std::u16string, std::u16string>, std::size_t> indexes;

            for (const auto& module : graph.modules) {
                if (!module.exit) {
                    continue;
                }

                for (const auto& branch : module.exit->branches) {
                    std::optional<std::u16string> target_chapter;

                    if (branch.target == graph.end) {
                        target_chapter = graph.end;
                    } else {
                        const auto target = modules_by_id.find(branch.target);
                        if (target != modules_by_id.end()) {
                            target_chapter = target->second->chapter_id;
                        }
                    }

                    if (!target_chapter || *target_chapter == module.chapter_id) {
                        continue;
                    }

                    const auto key = std::make_pair(module.chapter_id, *target_chapter);
                    auto found = indexes.find(key);
                    std::size_t index = 0;

                    if (found == indexes.end()) {
                        index = edges.size();
                        indexes.emplace(key, index);
                        edges.push_back(ChapterEdge{
                            module.chapter_id, *target_chapter, {}});
                    } else {
                        index = found->second;
                    }

                    edges[index].details.push_back(ChapterEdgeDetail{
                        module.id,
                        branch.target,
                        module.exit->mode,
                        branch.label,
                        branch.condition,
                        branch.source,
                    });
                }
            }

            return edges;
        }

    } // namespace

    GraphAnalysis build_graph_analysis(
        const ProjectModel& model,
        const StoryGraph& graph)
    {
        const auto modules_by_id = by_id(graph);
        auto adjacency = build_adjacency(graph);
        auto reverse_adjacency = build_reverse_adjacency(adjacency);

        return GraphAnalysis{
            compute_reachable(graph, modules_by_id, adjacency),
            compute_can_reach_end(graph, reverse_adjacency),
            build_references(model, graph),
            std::move(adjacency),
            std::move(reverse_adjacency),
            build_chapter_edges(graph, modules_by_id),
        };
    }

} // namespace weftline::ir
