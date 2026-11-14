#include "weftline/syntax/ast_printer.hpp"

#include <cstddef>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include "weftline/source/source_text.hpp"
#include "weftline/support/overloaded.hpp"

namespace weftline::syntax {
    namespace {

        // 调试输出使用固定 4 空格缩进；AST 打印不参与编译流程。
        void append_line(std::u16string& out, int depth, std::u16string_view text)
        {
            out.append(static_cast<std::size_t>(depth) * 4, u' ');
            out.append(text);
            out.push_back(u'\n');
        }

        // 把正文压成单行并截断，避免调试输出被大段文本淹没。
        std::u16string short_text(std::u16string_view text)
        {
            std::u16string flattened;
            flattened.reserve(text.size());

            for (char16_t value : text) {
                if (value == u'\r') {
                    flattened.push_back(u' ');
                } else if (value == u'\n') {
                    flattened += u" | ";
                } else {
                    flattened.push_back(value);
                }
            }

            if (flattened.size() <= 60) {
                return flattened;
            }

            return flattened.substr(0, 57) + u"...";
        }

        std::u16string format_segments(const std::vector<TextSegment>& segments)
        {
            std::u16string result;

            for (const auto& segment : segments) {
                std::visit(
                    support::overloaded{
                        [&](const TextLiteral& literal) { result += literal.text; },
                        [&](const TextReference& reference) {
                            result.push_back(u'{');
                            result += reference.name;
                            result.push_back(u'}');
                        },
                    },
                    segment);
            }

            return result;
        }

        std::u16string format_condition(const BoolExpr& expression)
        {
            return std::visit(
                support::overloaded{
                    [](const BoolLiteral& literal) -> std::u16string {
                        return literal.value ? std::u16string(u"true") : std::u16string(u"false");
                    },
                    [](const BoolRef& reference) -> std::u16string {
                        return reference.name;
                    },
                    [](const NotExpr& expr) -> std::u16string {
                        std::u16string result = u"not (";
                        result += format_condition(*expr.operand);
                        result.push_back(u')');
                        return result;
                    },
                    [](const AndExpr& expr) -> std::u16string {
                        std::u16string result = u"(";
                        result += format_condition(*expr.left);
                        result += u" and ";
                        result += format_condition(*expr.right);
                        result.push_back(u')');
                        return result;
                    },
                    [](const OrExpr& expr) -> std::u16string {
                        std::u16string result = u"(";
                        result += format_condition(*expr.left);
                        result += u" or ";
                        result += format_condition(*expr.right);
                        result.push_back(u')');
                        return result;
                    },
                },
                expression.node);
        }

        std::u16string description(const std::optional<TextBlock>& block)
        {
            if (!block) {
                return {};
            }

            std::u16string result = u"  [";
            result += short_text(block->text);
            result.push_back(u']');
            return result;
        }

        std::u16string format_content(const Content& content)
        {
            return std::visit(
                support::overloaded{
                    [](const Narration& narration) -> std::u16string {
                        std::u16string result = u"[";
                        result += format_segments(narration.segments);
                        result.push_back(u']');
                        return result;
                    },
                    [](const Dialogue& dialogue) -> std::u16string {
                        std::u16string result = dialogue.speaker;
                        result += u":[";
                        result += format_segments(dialogue.segments);
                        result.push_back(u']');
                        return result;
                    },
                    [](const Figure& figure) -> std::u16string {
                        std::u16string result = u"@fig \"";
                        result += figure.path;
                        result.push_back(u'"');
                        return result;
                    },
                },
                content);
        }

        // 场景标记和正文按 content_index 交错输出，保持源顺序。
        void append_module_content(
            std::u16string& out, const Module& module)
        {
            std::size_t scene_index = 0;

            for (std::size_t content_index = 0;
                 content_index <= module.content.size();
                 ++content_index) {
                while (scene_index < module.scenes.size()
                       && module.scenes[scene_index].content_index == content_index) {
                    std::u16string line = u"scene ";
                    line += module.scenes[scene_index].name;
                    append_line(out, 2, line);
                    ++scene_index;
                }

                if (content_index < module.content.size()) {
                    append_line(out, 2, format_content(module.content[content_index]));
                }
            }
        }

        // .wstory 只打印项目信息、章节、声明和文档；描述保留原文便于核对。
        void print_wstory(std::u16string& out, const WStoryFile& wstory)
        {
            std::u16string header = u"WStory \"";
            header += wstory.project_name;
            header.push_back(u'"');
            append_line(out, 0, header);

            if (wstory.summary) {
                std::u16string line = u"summary: [";
                line += short_text(wstory.summary->text);
                line.push_back(u']');
                append_line(out, 1, line);
            }

            for (const auto& doc : wstory.docs) {
                std::u16string line = u"doc \"";
                line += doc.title;
                line += u"\": [";
                line += short_text(doc.body.text);
                line.push_back(u']');
                append_line(out, 1, line);
            }

            if (!wstory.characters.empty()) {
                append_line(out, 1, u"chars");
                for (const auto& character : wstory.characters) {
                    std::u16string line = u"char ";
                    line += character.name;
                    line += description(character.description);
                    append_line(out, 2, line);
                }
            }

            if (!wstory.items.empty()) {
                append_line(out, 1, u"items");
                for (const auto& item : wstory.items) {
                    std::u16string line = u"item ";
                    line += item.name;
                    line += description(item.description);
                    append_line(out, 2, line);
                }
            }

            if (!wstory.scenes.empty()) {
                append_line(out, 1, u"scenes");
                for (const auto& scene : wstory.scenes) {
                    std::u16string line = u"scene ";
                    line += scene.name;
                    line += description(scene.description);
                    append_line(out, 2, line);
                }
            }

            if (!wstory.variables.empty()) {
                append_line(out, 1, u"vars");
                for (const auto& variable : wstory.variables) {
                    std::u16string line = u"var ";
                    line += variable.name;
                    line += u" = ";
                    line += variable.value ? u"true" : u"false";
                    line += description(variable.description);
                    append_line(out, 2, line);
                }
            }
        }

        // .weft 按源顺序打印声明与模块，出口块展开成分支列表。
        void print_weft(std::u16string& out, const WeftFile& weft)
        {
            append_line(out, 0, u"WeftFile");

            if (!weft.variables.empty()) {
                append_line(out, 1, u"vars");
                for (const auto& variable : weft.variables) {
                    std::u16string line = u"var ";
                    line += variable.name;
                    line += u" = ";
                    line += variable.value ? u"true" : u"false";
                    append_line(out, 2, line);
                }
            }

            if (!weft.characters.empty()) {
                append_line(out, 1, u"chars");
                for (const auto& character : weft.characters) {
                    std::u16string line = u"char ";
                    line += character.name;
                    append_line(out, 2, line);
                }
            }

            if (!weft.items.empty()) {
                append_line(out, 1, u"items");
                for (const auto& item : weft.items) {
                    std::u16string line = u"item ";
                    line += item.name;
                    append_line(out, 2, line);
                }
            }

            if (!weft.scenes.empty()) {
                append_line(out, 1, u"scenes");
                for (const auto& scene : weft.scenes) {
                    std::u16string line = u"scene ";
                    line += scene.name;
                    append_line(out, 2, line);
                }
            }

            for (const auto& module : weft.modules) {
                std::u16string header = u"Module \"";
                header += module.title;
                header.push_back(u'"');
                append_line(out, 1, header);

                append_module_content(out, module);

                if (!module.exit) {
                    append_line(out, 2, u"@next <missing>");
                    continue;
                }

                std::u16string exit_line = u"@next ";
                if (module.exit->is_choice_mode()) {
                    exit_line += u"choice (";
                    exit_line += *module.exit->character;
                    exit_line.push_back(u')');
                } else {
                    exit_line += u"auto";
                }
                append_line(out, 2, exit_line);

                for (const auto& branch : module.exit->branches) {
                    std::u16string line;
                    if (branch.label) {
                        line.push_back(u'[');
                        line += *branch.label;
                        line += u"] ";
                    }
                    if (branch.condition) {
                        line += u"when ";
                        line += format_condition(*branch.condition);
                        line.push_back(u' ');
                    }
                    line += u"-> ";
                    line += branch.target;

                    if (!branch.effects.empty()) {
                        line += u" { ";
                        for (std::size_t index = 0; index < branch.effects.size(); ++index) {
                            if (index != 0) {
                                line += u"; ";
                            }
                            line += branch.effects[index].name;
                            line += branch.effects[index].value ? u" = true" : u" = false";
                        }
                        line += u" }";
                    }

                    append_line(out, 3, line);
                }
            }
        }

    } // namespace

    std::u16string print_ast(const ScriptFile& file)
    {
        std::u16string out;

        std::visit(
            support::overloaded{
                [&](const WeftFile& weft) { print_weft(out, weft); },
                [&](const WStoryFile& wstory) { print_wstory(out, wstory); },
            },
            file);

        return out;
    }

    std::string print_ast_utf8(const ScriptFile& file)
    {
        return source::utf16_to_utf8(print_ast(file));
    }

} // namespace weftline::syntax
