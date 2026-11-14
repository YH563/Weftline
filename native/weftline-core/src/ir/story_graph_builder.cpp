#include "weftline/ir/story_graph_builder.hpp"

#include <algorithm>
#include <memory>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_set>
#include <utility>
#include <vector>

#include "weftline/ir/project_model_builder.hpp"
#include "weftline/projects/project_entry.hpp"
#include "weftline/support/overloaded.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::ir {

    IrBoolExpr IrBoolExpr::literal(bool value)
    {
        IrBoolExpr expression;
        expression.kind = BoolExprKind::Literal;
        expression.value = value;
        return expression;
    }

    IrBoolExpr IrBoolExpr::ref(std::u16string name)
    {
        IrBoolExpr expression;
        expression.kind = BoolExprKind::Ref;
        expression.name = std::move(name);
        return expression;
    }

    IrBoolExpr IrBoolExpr::not_(IrBoolExpr operand)
    {
        IrBoolExpr expression;
        expression.kind = BoolExprKind::Not;
        expression.operand = std::make_shared<const IrBoolExpr>(std::move(operand));
        return expression;
    }

    IrBoolExpr IrBoolExpr::and_(IrBoolExpr left, IrBoolExpr right)
    {
        IrBoolExpr expression;
        expression.kind = BoolExprKind::And;
        expression.left = std::make_shared<const IrBoolExpr>(std::move(left));
        expression.right = std::make_shared<const IrBoolExpr>(std::move(right));
        return expression;
    }

    IrBoolExpr IrBoolExpr::or_(IrBoolExpr left, IrBoolExpr right)
    {
        IrBoolExpr expression;
        expression.kind = BoolExprKind::Or;
        expression.left = std::make_shared<const IrBoolExpr>(std::move(left));
        expression.right = std::make_shared<const IrBoolExpr>(std::move(right));
        return expression;
    }

    IrTextSegment IrTextSegment::literal(std::u16string text)
    {
        IrTextSegment segment;
        segment.kind = TextSegmentKind::Text;
        segment.text = std::move(text);
        return segment;
    }

    IrTextSegment IrTextSegment::reference(std::u16string name)
    {
        IrTextSegment segment;
        segment.kind = TextSegmentKind::Ref;
        segment.name = std::move(name);
        return segment;
    }

    IrContentBlock IrContentBlock::narration(std::vector<IrTextSegment> segments)
    {
        IrContentBlock block;
        block.kind = ContentKind::Narration;
        block.segments = std::move(segments);
        return block;
    }

    IrContentBlock IrContentBlock::dialogue(
        std::u16string speaker, std::vector<IrTextSegment> segments)
    {
        IrContentBlock block;
        block.kind = ContentKind::Dialogue;
        block.speaker = std::move(speaker);
        block.segments = std::move(segments);
        return block;
    }

    IrContentBlock IrContentBlock::figure(std::u16string path)
    {
        IrContentBlock block;
        block.kind = ContentKind::Figure;
        block.path = std::move(path);
        return block;
    }

    namespace {

        // 语法 AST 的 bool 表达式是递归 variant；这里转成 IR 的扁平 Kind + shared_ptr 子节点。
        [[nodiscard]] IrBoolExpr to_bool_expr(const syntax::BoolExpr& expression)
        {
            return std::visit(
                support::overloaded{
                    [](const syntax::BoolLiteral& literal) {
                        return IrBoolExpr::literal(literal.value);
                    },
                    [](const syntax::BoolRef& reference) {
                        return IrBoolExpr::ref(reference.name);
                    },
                    [](const syntax::NotExpr& expr) {
                        return IrBoolExpr::not_(to_bool_expr(*expr.operand));
                    },
                    [](const syntax::AndExpr& expr) {
                        return IrBoolExpr::and_(
                            to_bool_expr(*expr.left),
                            to_bool_expr(*expr.right));
                    },
                    [](const syntax::OrExpr& expr) {
                        return IrBoolExpr::or_(
                            to_bool_expr(*expr.left),
                            to_bool_expr(*expr.right));
                    },
                },
                expression.node);
        }

        // {名字} 保留为引用片段，HTML/筛选阶段再决定它代表角色还是道具。
        [[nodiscard]] std::vector<IrTextSegment> to_segments(
            const std::vector<syntax::TextSegment>& segments)
        {
            std::vector<IrTextSegment> out;
            out.reserve(segments.size());

            for (const auto& segment : segments) {
                std::visit(
                    support::overloaded{
                        [&](const syntax::TextLiteral& literal) {
                            out.push_back(IrTextSegment::literal(literal.text));
                        },
                        [&](const syntax::TextReference& reference) {
                            out.push_back(IrTextSegment::reference(reference.name));
                        },
                    },
                    segment);
            }

            return out;
        }

        // 语法内容块 → IR 内容块：旁白/对话/图片三种保持不变。
        [[nodiscard]] IrContentBlock to_content_block(const syntax::Content& content)
        {
            return std::visit(
                support::overloaded{
                    [](const syntax::Narration& narration) {
                        return IrContentBlock::narration(to_segments(narration.segments));
                    },
                    [](const syntax::Dialogue& dialogue) {
                        return IrContentBlock::dialogue(
                            dialogue.speaker, to_segments(dialogue.segments));
                    },
                    [](const syntax::Figure& figure) {
                        return IrContentBlock::figure(figure.path);
                    },
                },
                content);
        }

        // 单个模块：正文、场景标记、可选出口块和源码范围；模块 ID 就是节点 ID。
        [[nodiscard]] IrModule build_module(
            std::string_view file_path,
            const std::u16string& chapter_id,
            const syntax::Module& module)
        {
            std::vector<IrSceneMarker> scenes;
            scenes.reserve(module.scenes.size());
            for (const auto& scene : module.scenes) {
                scenes.push_back(IrSceneMarker{
                    scene.name,
                    scene.content_index,
                });
            }

            std::vector<IrContentBlock> content;
            content.reserve(module.content.size());
            for (const auto& block : module.content) {
                content.push_back(to_content_block(block));
            }

            std::optional<IrExitBlock> exit;
            if (module.exit) {
                const auto& exit_syntax = *module.exit;
                std::vector<IrExitBranch> branches;
                branches.reserve(exit_syntax.branches.size());

                for (const auto& branch : exit_syntax.branches) {
                    std::optional<IrBoolExpr> condition;
                    if (branch.condition) {
                        condition = to_bool_expr(*branch.condition);
                    }

                    std::vector<IrAssignment> effects;
                    effects.reserve(branch.effects.size());
                    for (const auto& effect : branch.effects) {
                        effects.push_back(IrAssignment{effect.name, effect.value});
                    }

                    branches.push_back(IrExitBranch{
                        std::move(condition),
                        branch.label,
                        branch.target,
                        std::move(effects),
                        source::SourceLocation{std::string(file_path), branch.span},
                    });
                }

                exit = IrExitBlock{
                    exit_syntax.is_choice_mode() ? ExitMode::Choice : ExitMode::Auto,
                    exit_syntax.character,
                    std::move(branches),
                    source::SourceLocation{std::string(file_path), exit_syntax.span},
                };
            }

            return IrModule{
                module.title,
                chapter_id,
                std::move(scenes),
                std::move(content),
                std::move(exit),
                source::SourceLocation{std::string(file_path), module.title_span},
            };
        }

    } // namespace

    // 按文件顺序展开模块；同名模块只保留第一次出现，避免重复节点。
    StoryGraph build_story_graph(
        const projects::LoadedProject& project,
        const ProjectModel& model,
        std::optional<std::u16string> entry)
    {
        std::vector<IrModule> modules;
        std::unordered_set<std::u16string> seen_module_ids;

        for (const auto& file : project.weft_files) {
            for (const auto& module : file.ast->modules) {
                if (!seen_module_ids.insert(module.title).second) {
                    continue;
                }
                modules.push_back(build_module(file.file_path, file.ast->title, module));
            }
        }

        std::vector<IrGlobal> globals;
        globals.reserve(model.symbols.variables.size());
        for (const auto& variable : model.symbols.variables) {
            globals.push_back(IrGlobal{variable.name, variable.initial});
        }

        const auto entries = projects::resolve_entries(project);

        // 没有显式入口时，默认使用第一个模块；候选入口是无入边的其他模块。
        std::optional<std::u16string> resolved_entry;
        if (entry) {
            resolved_entry = std::move(*entry);
        } else if (entries.default_entry) {
            resolved_entry = *entries.default_entry;
        } else if (!modules.empty()) {
            resolved_entry = modules.front().id;
        }

        auto candidates = entries.candidates;
        if (resolved_entry) {
            candidates.erase(
                std::remove(candidates.begin(), candidates.end(), *resolved_entry),
                candidates.end());
        }

        return StoryGraph{
            2,
            u"END",
            std::move(resolved_entry),
            std::move(candidates),
            std::move(globals),
            std::move(modules),
        };
    }

    StoryGraph build_story_graph(
        const projects::LoadedProject& project,
        const projects::ProjectIndex& index,
        std::optional<std::u16string> entry)
    {
        const auto model = build_project_model(project, index);
        return build_story_graph(project, model, std::move(entry));
    }

} // namespace weftline::ir
