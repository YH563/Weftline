#include "weftline/syntax/parser.hpp"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <set>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#include "weftline/source/text_utils.hpp"
#include "weftline/syntax/condition_parser.hpp"
#include "weftline/syntax/syntax_facts.hpp"

namespace weftline::syntax {
    namespace {

        constexpr auto npos = std::u16string_view::npos;

        // 行导向的手写解析器：按行扫描，@ 指令决定后续区域，正文块跨行收集。
        // 解析错误写入 DiagnosticBag 后尽量继续，不抛异常。
        class WeftParser
        {
        public:
            WeftParser(
                const source::SourceText& source,
                diagnostics::DiagnosticBag& diagnostics) noexcept
                : source_(source)
                , diagnostics_(diagnostics)
            {
            }

            [[nodiscard]] bool is_wstory_file() const
            {
                const auto& path = source_.file_path();
                constexpr std::string_view suffix = ".wstory";
                if (path.size() >= suffix.size()
                    && path.compare(path.size() - suffix.size(), suffix.size(), suffix) == 0) {
                    return true;
                }

                for (const auto& line : source_.lines()) {
                    const auto trimmed = source::trim(source_.line_text(line));
                    if (trimmed.empty() || source::starts_with(trimmed, u"//")) {
                        continue;
                    }
                    return is_directive(trimmed, u"@project");
                }

                return false;
            }

            // .weft 结构：@title → 顶部声明区 → 若干 @module。
            [[nodiscard]] WeftFile parse_weft()
            {
                skip_blank_and_comments();

                std::u16string chapter_title;
                source::TextSpan chapter_title_span =
                    current() ? current()->span : source::TextSpan{};

                if (is_at_end() || !is_directive(current_trimmed(), u"@title")) {
                    error(
                        diagnostics::DiagnosticCode::MissingChapterTitle,
                        chapter_title_span,
                        u".weft 第一行必须是章节标题 @title。");
                } else {
                    const auto title_line = *current();
                    advance();

                    const auto trimmed = source::trim(source_.line_text(title_line));
                    chapter_title = trimmed.size() > 6
                        ? std::u16string(source::trim(trimmed.substr(6)))
                        : std::u16string{};

                    if (chapter_title.empty()) {
                        error(
                            diagnostics::DiagnosticCode::EmptyTitle,
                            title_line.span,
                            u"@title 不能为空。");
                        chapter_title_span = title_line.span;
                    } else {
                        chapter_title_span =
                            span_after(title_line, u"@title", chapter_title);
                    }
                }

                std::vector<WeftVariable> variables;
                std::vector<WeftCharacter> characters;
                std::vector<WeftItem> items;
                std::vector<WeftScene> scenes;

                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        break;
                    }

                    const auto line = *current();
                    const auto trimmed = current_trimmed();

                    if (is_directive(trimmed, u"@vars")) {
                        advance();
                        parse_weft_variables(variables);
                        continue;
                    }
                    if (is_directive(trimmed, u"@chars")) {
                        advance();
                        parse_weft_names(
                            u"@char", characters,
                            [](std::u16string name, source::TextSpan span) {
                                return WeftCharacter{std::move(name), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@items")) {
                        advance();
                        parse_weft_names(
                            u"@item", items,
                            [](std::u16string name, source::TextSpan span) {
                                return WeftItem{std::move(name), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@scenes")) {
                        advance();
                        parse_weft_names(
                            u"@scene", scenes,
                            [](std::u16string name, source::TextSpan span) {
                                return WeftScene{std::move(name), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@var")) {
                        parse_weft_variable_line(line, trimmed, variables);
                        continue;
                    }
                    if (is_directive(trimmed, u"@char")) {
                        parse_weft_name_line(
                            line, trimmed, u"@char", characters,
                            [](std::u16string name, source::TextSpan span) {
                                return WeftCharacter{std::move(name), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@item")) {
                        parse_weft_name_line(
                            line, trimmed, u"@item", items,
                            [](std::u16string name, source::TextSpan span) {
                                return WeftItem{std::move(name), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@scene")) {
                        parse_weft_name_line(
                            line, trimmed, u"@scene", scenes,
                            [](std::u16string name, source::TextSpan span) {
                                return WeftScene{std::move(name), span};
                            });
                        continue;
                    }

                    break;
                }

                std::vector<Module> modules;

                while (!is_at_end()) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        break;
                    }

                    const auto line = *current();
                    const auto trimmed = current_trimmed();

                    if (is_directive(trimmed, u"@module")) {
                        modules.push_back(parse_module());
                        continue;
                    }
                    if (is_directive(trimmed, u"@title")) {
                        error(
                            diagnostics::DiagnosticCode::MultipleChapterTitle,
                            line.span,
                            u"一个 .weft 文件只能有一个章节标题 @title。");
                        advance();
                        continue;
                    }
                    if (is_declaration_directive(trimmed)) {
                        error(
                            diagnostics::DiagnosticCode::VarOutsideGlobals,
                            line.span,
                            u"符号声明必须放在第一个 @module 之前。");
                        advance();
                        continue;
                    }
                    if (is_project_directive(trimmed)) {
                        error(
                            diagnostics::DiagnosticCode::MalformedDirective,
                            line.span,
                            u"项目指令只能出现在 .wstory 文件中。");
                        advance();
                        continue;
                    }

                    error(
                        diagnostics::DiagnosticCode::UnknownLine,
                        line.span,
                        u"无法识别的行；模块必须以 @module 开始。");
                    advance();
                }

                return WeftFile{
                    std::move(chapter_title),
                    chapter_title_span,
                    std::move(variables),
                    std::move(characters),
                    std::move(items),
                    std::move(scenes),
                    std::move(modules),
                    source::TextSpan{0, source_.length()},
                };
            }

            // .wstory 结构：@project → 可选的概要/章节/文档/声明描述。
            [[nodiscard]] WStoryFile parse_wstory()
            {
                WStoryFile wstory;
                wstory.span = source::TextSpan{0, source_.length()};

                skip_blank_and_comments();
                if (is_at_end() || !is_directive(current_trimmed(), u"@project")) {
                    error(
                        diagnostics::DiagnosticCode::MalformedDirective,
                        current() ? current()->span : source::TextSpan{},
                        u".wstory 必须以 @project 开始。");
                    return wstory;
                }

                const auto project_line = *current();
                advance();

                const auto project_trimmed =
                    source::trim(source_.line_text(project_line));
                wstory.project_name = project_trimmed.size() > 8
                    ? std::u16string(source::trim(project_trimmed.substr(8)))
                    : std::u16string{};

                if (wstory.project_name.empty()) {
                    error(
                        diagnostics::DiagnosticCode::EmptyTitle,
                        project_line.span,
                        u"@project 不能为空。");
                    wstory.project_name_span = project_line.span;
                } else {
                    wstory.project_name_span =
                        span_after(project_line, u"@project", wstory.project_name);
                }

                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        break;
                    }

                    const auto line = *current();
                    const auto trimmed = current_trimmed();

                    if (is_directive(trimmed, u"@summary")) {
                        wstory.summary = parse_summary(line);
                        continue;
                    }
                    if (is_directive(trimmed, u"@chapters")) {
                        advance();
                        parse_chapters(wstory.chapters);
                        continue;
                    }
                    if (is_directive(trimmed, u"@docs")) {
                        advance();
                        parse_docs(wstory.docs);
                        continue;
                    }
                    if (is_directive(trimmed, u"@chars")) {
                        advance();
                        parse_wstory_names(
                            u"@char", wstory.characters,
                            [](std::u16string name,
                               std::optional<TextBlock> description,
                               source::TextSpan span) {
                                return WStoryCharacter{
                                    std::move(name), std::move(description), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@items")) {
                        advance();
                        parse_wstory_names(
                            u"@item", wstory.items,
                            [](std::u16string name,
                               std::optional<TextBlock> description,
                               source::TextSpan span) {
                                return WStoryItem{
                                    std::move(name), std::move(description), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@scenes")) {
                        advance();
                        parse_wstory_names(
                            u"@scene", wstory.scenes,
                            [](std::u16string name,
                               std::optional<TextBlock> description,
                               source::TextSpan span) {
                                return WStoryScene{
                                    std::move(name), std::move(description), span};
                            });
                        continue;
                    }
                    if (is_directive(trimmed, u"@vars")) {
                        advance();
                        parse_wstory_variables(wstory.variables);
                        continue;
                    }
                    if (is_directive(trimmed, u"@set")) {
                        error(
                            diagnostics::DiagnosticCode::SetInProjectFile,
                            line.span,
                            u".wstory 不允许 @set；变量初始值由 .weft 的 @var 决定。");
                        advance();
                        continue;
                    }
                    if (is_directive(trimmed, u"@title")
                        || is_directive(trimmed, u"@next")
                        || is_directive(trimmed, u"@chapter")
                        || source::starts_with(trimmed, u"[")) {
                        error(
                            diagnostics::DiagnosticCode::MalformedDirective,
                            line.span,
                            u".wstory 只能包含 @project、@summary、@chapters、@docs、@chars、@items、@scenes 和 @vars。");
                        advance();
                        continue;
                    }

                    error(
                        diagnostics::DiagnosticCode::UnknownLine,
                        line.span,
                        u"无法识别的 .wstory 行。");
                    advance();
                }

                return wstory;
            }

            // @summary [文本]：方括号内可以单行，也可以跨行。
            [[nodiscard]] TextBlock parse_summary(const source::SourceLine& line)
            {
                const auto text = source_.line_text(line);
                const auto bracket = source::index_of(text, u'[');
                if (bracket == npos) {
                    error(
                        diagnostics::DiagnosticCode::MalformedDirective,
                        line.span,
                        u"@summary 需要写成 @summary [文本]。");
                    advance();
                    return TextBlock{u"", line.span};
                }

                return parse_plain_text_block_from(line, bracket);
            }

            // 连续读取 @chapters 下的 @chapter 条目，遇到其他指令即停止。
            void parse_chapters(std::vector<WStoryChapter>& chapters)
            {
                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        return;
                    }

                    const auto line = *current();
                    const auto text = source_.line_text(line);
                    const auto trimmed = source::trim(text);
                    if (!is_directive(trimmed, u"@chapter")) {
                        return;
                    }

                    const auto bracket = source::index_of(text, u'[');
                    const auto declaration_end = bracket != npos
                        ? source::index_of(trimmed, u'[')
                        : trimmed.size();

                    const auto title = declaration_end > 8
                        ? std::u16string(source::trim(
                            trimmed.substr(8, declaration_end - 8)))
                        : std::u16string{};
                    const auto title_span = title.empty()
                        ? line.span
                        : span_after(line, u"@chapter", title);

                    std::optional<TextBlock> description;
                    std::int32_t end = line.span.end();

                    if (bracket != npos) {
                        description = parse_plain_text_block_from(line, bracket);
                        end = description->span.end();
                    } else {
                        advance();
                    }

                    if (title.empty()) {
                        error(
                            diagnostics::DiagnosticCode::EmptyTitle,
                            line.span,
                            u"@chapter 需要一个标题。");
                    }

                    chapters.push_back(WStoryChapter{
                        title, title_span, std::move(description),
                        source::TextSpan::from_bounds(line.span.start, end)});
                }
            }

            // @docs 下连续读取 @doc <标题> [正文]。
            void parse_docs(std::vector<Doc>& docs)
            {
                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        return;
                    }

                    const auto line = *current();
                    const auto text = source_.line_text(line);
                    const auto trimmed = source::trim(text);
                    if (!is_directive(trimmed, u"@doc")) {
                        return;
                    }

                    const auto bracket = source::index_of(text, u'[');
                    const auto declaration_end = bracket != npos
                        ? source::index_of(trimmed, u'[')
                        : trimmed.size();

                    const auto title = declaration_end > 4
                        ? std::u16string(source::trim(
                            trimmed.substr(4, declaration_end - 4)))
                        : std::u16string{};
                    const auto title_span = title.empty()
                        ? line.span
                        : span_after(line, u"@doc", title);

                    TextBlock body{u"", line.span};
                    std::int32_t end = line.span.end();

                    if (bracket != npos) {
                        body = parse_plain_text_block_from(line, bracket);
                        end = body.span.end();
                    } else {
                        advance();
                        error(
                            diagnostics::DiagnosticCode::MalformedDirective,
                            line.span,
                            u"@doc 需要写成 @doc <标题> [文本]。");
                    }

                    if (title.empty()) {
                        error(
                            diagnostics::DiagnosticCode::EmptyTitle,
                            line.span,
                            u"@doc 需要一个标题。");
                    }

                    docs.push_back(Doc{
                        title, title_span, std::move(body),
                        source::TextSpan::from_bounds(line.span.start, end)});
                }
            }

            // .wstory 的角色/道具/场景声明共用：名称 + 可选描述。
            template <typename T, typename Factory>
            void parse_wstory_names(
                std::u16string_view keyword,
                std::vector<T>& target,
                Factory factory)
            {
                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        return;
                    }

                    const auto line = *current();
                    const auto text = source_.line_text(line);
                    const auto trimmed = source::trim(text);
                    if (!is_directive(trimmed, keyword)) {
                        return;
                    }

                    const auto bracket = source::index_of(text, u'[');
                    const auto declaration_end = bracket != npos
                        ? source::index_of(trimmed, u'[')
                        : trimmed.size();

                    const auto entry = declaration_end > keyword.size()
                        ? std::u16string(source::trim(trimmed.substr(
                            keyword.size(), declaration_end - keyword.size())))
                        : std::u16string{};

                    std::optional<TextBlock> description;
                    std::int32_t end = line.span.end();

                    if (bracket != npos) {
                        description = parse_plain_text_block_from(line, bracket);
                        end = description->span.end();
                    } else {
                        advance();
                    }

                    if (!is_identifier(entry)) {
                        std::u16string message(keyword);
                        message += u" 的语法是：";
                        message += keyword;
                        message += u" <名称> [描述]。";
                        error(
                            diagnostics::DiagnosticCode::MalformedDirective,
                            line.span,
                            std::move(message));
                        continue;
                    }

                    target.push_back(factory(
                        std::move(entry),
                        std::move(description),
                        source::TextSpan::from_bounds(line.span.start, end)));
                }
            }

            // .wstory 变量只提供描述和初始值；声明来源仍是 .weft。
            void parse_wstory_variables(std::vector<WStoryVariable>& variables)
            {
                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        return;
                    }

                    const auto line = *current();
                    const auto text = source_.line_text(line);
                    const auto trimmed = source::trim(text);
                    if (!is_directive(trimmed, u"@var")) {
                        return;
                    }

                    const auto bracket = source::index_of(text, u'[');
                    const auto declaration_end = bracket != npos
                        ? source::index_of(trimmed, u'[')
                        : trimmed.size();

                    const auto declaration = declaration_end > 4
                        ? source::trim(trimmed.substr(4, declaration_end - 4))
                        : std::u16string_view{};

                    std::optional<TextBlock> description;
                    std::int32_t end = line.span.end();

                    if (bracket != npos) {
                        description = parse_plain_text_block_from(line, bracket);
                        end = description->span.end();
                    } else {
                        advance();
                    }

                    const auto parts = source::split_whitespace(declaration);
                    bool value = false;
                    if (parts.size() != 2
                        || !is_identifier(parts[0])
                        || !try_parse_boolean(parts[1], value)) {
                        error(
                            diagnostics::DiagnosticCode::MalformedDirective,
                            line.span,
                            u"@var 的语法是：@var <布尔变量名> true|false [描述]。");
                        continue;
                    }

                    variables.push_back(WStoryVariable{
                        std::u16string(parts[0]),
                        value,
                        std::move(description),
                        source::TextSpan::from_bounds(line.span.start, end)});
                }
            }

            // 从 '[' 开始读取文本块：优先匹配同一行 ']'，否则逐行收集到闭合行。
            [[nodiscard]] TextBlock parse_plain_text_block_from(
                const source::SourceLine& opening_line,
                std::size_t open_bracket)
            {
                const auto text = source_.line_text(opening_line);
                const auto close = source::index_of(text, u']', open_bracket + 1);

                if (close != npos) {
                    const auto inline_text = source::trim(
                        text.substr(open_bracket + 1, close - open_bracket - 1));
                    advance();
                    return TextBlock{
                        std::u16string(inline_text), opening_line.span};
                }

                std::vector<std::u16string> lines;
                lines.push_back(std::u16string(
                    source::trim(text.substr(open_bracket + 1))));

                auto end = opening_line.span.end();
                advance();

                bool closed = false;

                while (!is_at_end()) {
                    const auto line = *current();
                    const auto line_text = source_.line_text(line);
                    const auto close_index = source::index_of(line_text, u']');

                    if (close_index != npos) {
                        lines.push_back(std::u16string(
                            source::trim(line_text.substr(0, close_index))));
                        end = line.span.start
                            + static_cast<std::int32_t>(close_index) + 1;
                        closed = true;
                        advance();
                        break;
                    }

                    lines.push_back(std::u16string(source::trim(line_text)));
                    end = line.span.end();
                    advance();
                }

                if (!closed) {
                    error(
                        diagnostics::DiagnosticCode::UnterminatedTextBlock,
                        opening_line.span,
                        u"文本块缺少闭合的 ]。");
                    end = last_line_end();
                }

                while (!lines.empty() && lines.front().empty()) {
                    lines.erase(lines.begin());
                }
                while (!lines.empty() && lines.back().empty()) {
                    lines.pop_back();
                }

                std::u16string joined;
                for (std::size_t index = 0; index < lines.size(); ++index) {
                    if (index != 0) {
                        joined.push_back(u'\n');
                    }
                    joined += lines[index];
                }

                return TextBlock{
                    std::move(joined),
                    source::TextSpan::from_bounds(opening_line.span.start, end)};
            }

        private:
            [[nodiscard]] const source::SourceLine* current() const noexcept
            {
                return index_ < source_.lines().size() ? &source_.lines()[index_] : nullptr;
            }

            [[nodiscard]] std::u16string_view current_text() const noexcept
            {
                const auto* line = current();
                return line ? source_.line_text(*line) : std::u16string_view{};
            }

            [[nodiscard]] std::u16string_view current_trimmed() const noexcept
            {
                return source::trim(current_text());
            }

            void advance() noexcept
            {
                ++index_;
            }

            [[nodiscard]] bool is_at_end() const noexcept
            {
                return index_ >= source_.lines().size();
            }

            [[nodiscard]] std::int32_t last_line_end() const noexcept
            {
                return source_.lines().empty() ? 0 : source_.lines().back().span.end();
            }

            // 解析器把空行和整行注释视为透明，不改变当前状态。
            void skip_blank_and_comments()
            {
                while (!is_at_end()) {
                    const auto trimmed = current_trimmed();
                    if (trimmed.empty() || source::starts_with(trimmed, u"//")) {
                        advance();
                        continue;
                    }
                    break;
                }
            }

            // 指令必须后面跟空白、'('、':' 或 '['，避免 @var 命中 @variable。
            [[nodiscard]] static bool is_directive(
                std::u16string_view trimmed, std::u16string_view keyword) noexcept
            {
                if (!source::starts_with(trimmed, keyword)) {
                    return false;
                }
                if (trimmed.size() == keyword.size()) {
                    return true;
                }
                const auto next = trimmed[keyword.size()];
                return is_space(next) || next == u'(' || next == u':' || next == u'[';
            }

            // 出口分支的三种开头：->、条件括号、以及带条件的 [文案] 行。
            [[nodiscard]] static bool is_branch_start(std::u16string_view trimmed) noexcept
            {
                if (source::starts_with(trimmed, u"->")) {
                    return true;
                }
                if (source::starts_with(trimmed, u"(")) {
                    return true;
                }
                if (source::starts_with(trimmed, u"[")) {
                    return source::index_of(trimmed, u"->") != npos;
                }
                return false;
            }

            [[nodiscard]] static bool is_declaration_directive(
                std::u16string_view trimmed) noexcept
            {
                return is_directive(trimmed, u"@globals")
                    || is_directive(trimmed, u"@vars")
                    || is_directive(trimmed, u"@chars")
                    || is_directive(trimmed, u"@items")
                    || is_directive(trimmed, u"@scenes")
                    || is_directive(trimmed, u"@var")
                    || is_directive(trimmed, u"@char")
                    || is_directive(trimmed, u"@item")
                    || is_directive(trimmed, u"@scene");
            }

            [[nodiscard]] static bool is_project_directive(
                std::u16string_view trimmed) noexcept
            {
                return is_directive(trimmed, u"@project")
                    || is_directive(trimmed, u"@summary")
                    || is_directive(trimmed, u"@doc")
                    || is_directive(trimmed, u"@chapter");
            }

            void error(
                std::string_view code,
                source::TextSpan span,
                std::u16string message)
            {
                diagnostics_.error(code, span, std::move(message));
            }

            void warning(
                std::string_view code,
                source::TextSpan span,
                std::u16string message)
            {
                diagnostics_.warning(code, span, std::move(message));
            }

            static void skip_whitespace(
                std::u16string_view text, std::size_t& position) noexcept
            {
                while (position < text.size() && is_space(text[position])) {
                    ++position;
                }
            }

            // 在指令行中定位 token 的源码 span；找不到时退回整行 span。
            [[nodiscard]] source::TextSpan span_after(
                const source::SourceLine& line,
                std::u16string_view keyword,
                std::u16string_view token) const
            {
                const auto text = source_.line_text(line);
                const auto keyword_index = source::index_of(text, keyword);
                const auto search_start =
                    keyword_index == npos ? 0 : keyword_index + keyword.size();
                const auto token_index = search_start <= text.size()
                    ? source::index_of(text, token, search_start)
                    : npos;

                return token_index == npos
                    ? line.span
                    : source::TextSpan{
                        line.span.start + static_cast<std::int32_t>(token_index),
                        static_cast<std::int32_t>(token.size())};
            }

            void parse_weft_variables(std::vector<WeftVariable>& variables)
            {
                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        return;
                    }

                    const auto line = *current();
                    const auto trimmed = current_trimmed();
                    if (!is_directive(trimmed, u"@var")) {
                        return;
                    }

                    parse_weft_variable_line(line, trimmed, variables);
                }
            }

            void parse_weft_variable_line(
                const source::SourceLine& line,
                std::u16string_view trimmed,
                std::vector<WeftVariable>& variables)
            {
                advance();

                std::u16string name;
                bool value = false;
                if (!try_parse_assignment(trimmed, u"@var", line, name, value)) {
                    return;
                }

                variables.push_back(WeftVariable{std::move(name), value, line.span});
            }

            template <typename T, typename Factory>
            void parse_weft_names(
                std::u16string_view keyword,
                std::vector<T>& target,
                Factory factory)
            {
                while (true) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        return;
                    }

                    const auto line = *current();
                    const auto trimmed = current_trimmed();
                    if (!is_directive(trimmed, keyword)) {
                        return;
                    }

                    parse_weft_name_line(line, trimmed, keyword, target, factory);
                }
            }

            template <typename T, typename Factory>
            void parse_weft_name_line(
                const source::SourceLine& line,
                std::u16string_view trimmed,
                std::u16string_view keyword,
                std::vector<T>& target,
                Factory factory)
            {
                advance();

                std::u16string name;
                if (!try_parse_single_name(trimmed, keyword, line, name)) {
                    return;
                }

                target.push_back(factory(std::move(name), line.span));
            }

            // 模块内部顺序：可选 @scene → 正文/图片 → 必须的 @next 出口块。
            [[nodiscard]] Module parse_module()
            {
                const auto module_line = *current();
                advance();

                const auto trimmed_module_line =
                    source::trim(source_.line_text(module_line));
                const auto module_id = trimmed_module_line.size() > 7
                    ? std::u16string(source::trim(trimmed_module_line.substr(7)))
                    : std::u16string{};

                validate_module_title(module_id, module_line);

                const auto title_span = module_id.empty()
                    ? module_line.span
                    : span_after(module_line, u"@module", module_id);

                std::vector<SceneMarker> scenes;
                std::vector<Content> content;
                auto module_end = module_line.span.end();
                std::optional<Exit> exit;

                while (!is_at_end()) {
                    skip_blank_and_comments();
                    if (is_at_end()) {
                        break;
                    }

                    const auto line = *current();
                    const auto trimmed = current_trimmed();

                    if (is_directive(trimmed, u"@module")
                        || is_directive(trimmed, u"@title")) {
                        break;
                    }

                    if (is_directive(trimmed, u"@next")) {
                        exit = parse_exit();
                        module_end = exit->span.end();
                        break;
                    }

                    if (is_directive(trimmed, u"@scene")) {
                        advance();
                        module_end = line.span.end();

                        const auto name =
                            source::trim(trimmed.substr(6));
                        if (!is_identifier(name)) {
                            error(
                                diagnostics::DiagnosticCode::MalformedScene,
                                line.span,
                                u"@scene 的语法是：@scene <场景名>");
                        } else {
                            scenes.push_back(SceneMarker{
                                std::u16string(name),
                                span_after(line, u"@scene", name),
                                content.size(),
                            });
                        }
                        continue;
                    }

                    if (is_declaration_directive(trimmed)
                        && !is_directive(trimmed, u"@scene")) {
                        error(
                            diagnostics::DiagnosticCode::VarOutsideGlobals,
                            line.span,
                            u"符号声明必须放在第一个 @module 之前。");
                        advance();
                        module_end = line.span.end();
                        continue;
                    }

                    if (is_directive(trimmed, u"@set")) {
                        error(
                            diagnostics::DiagnosticCode::SetInContent,
                            line.span,
                            u"正文不支持 @set；变量只能在出口分支效果中修改。");
                        advance();
                        module_end = line.span.end();
                        continue;
                    }

                    bool parsed = false;

                    if (source::starts_with(trimmed, u"[")) {
                        content.push_back(parse_narration());
                        parsed = true;
                    } else if (is_directive(trimmed, u"@fig")) {
                        auto figure = parse_figure();
                        if (figure) {
                            content.push_back(std::move(*figure));
                            parsed = true;
                        }
                    } else {
                        std::u16string speaker;
                        std::u16string_view rest;
                        if (try_get_dialogue_prefix(
                                source_.line_text(line), speaker, rest)) {
                            content.push_back(parse_dialogue(line, speaker));
                            parsed = true;
                        }
                    }

                    if (!parsed) {
                        error(
                            diagnostics::DiagnosticCode::UnknownLine,
                            line.span,
                            u"无法识别的行；旁白和对话必须写在 [] 中。");
                        advance();
                        continue;
                    }

                    module_end = std::max(
                        module_end,
                        std::visit([](const auto& node) { return node.span.end(); }, content.back()));
                }

                if (!exit) {
                    std::u16string message = u"模块“";
                    message += module_id;
                    message += u"”缺少 @next 出口块。";
                    error(
                        diagnostics::DiagnosticCode::MissingExitBlock,
                        module_line.span,
                        std::move(message));
                }

                return Module{
                    std::move(module_id),
                    title_span,
                    std::move(scenes),
                    std::move(content),
                    std::move(exit),
                    source::TextSpan::from_bounds(module_line.span.start, module_end),
                };
            }

            // 模块 ID 是全局唯一节点 ID；END 保留，不能作为普通模块。
            void validate_module_title(
                std::u16string_view title, const source::SourceLine& module_line)
            {
                if (title.empty()) {
                    error(
                        diagnostics::DiagnosticCode::EmptyTitle,
                        module_line.span,
                        u"@module 不能为空。");
                    return;
                }

                if (!is_identifier(title)) {
                    if (source::index_of(title, u'{') != npos
                        || source::index_of(title, u'}') != npos) {
                        error(
                            diagnostics::DiagnosticCode::InvalidTitleCharacter,
                            module_line.span,
                            u"@module 不能包含 { 或 }。");
                    } else {
                        error(
                            diagnostics::DiagnosticCode::MalformedDirective,
                            module_line.span,
                            u"@module 的语法是：@module <模块ID>");
                    }
                }

                if (title == u"END") {
                    error(
                        diagnostics::DiagnosticCode::InvalidEnd,
                        module_line.span,
                        u"END 是保留的终止模块，不能作为 @module 定义。");
                }

                if (!module_titles_.insert(std::u16string(title)).second) {
                    std::u16string message = u"模块 ID 重复：";
                    message += title;
                    error(
                        diagnostics::DiagnosticCode::DuplicateModuleTitle,
                        module_line.span,
                        std::move(message));
                }
            }

            // 旁白：整块正文都在 [] 内。
            [[nodiscard]] Narration parse_narration()
            {
                const auto opening_line = *current();
                const auto text = source_.line_text(opening_line);
                const auto open = source::index_of(text, u'[');
                std::int32_t end = opening_line.span.end();
                auto segments = parse_text_block_segments(opening_line, open, end);
                return Narration{
                    std::move(segments),
                    source::TextSpan::from_bounds(opening_line.span.start, end)};
            }

            // 对话：说话人 + ':' + [] 正文；说话人已由调用方切出。
            [[nodiscard]] Dialogue parse_dialogue(
                const source::SourceLine& opening_line, const std::u16string& speaker)
            {
                const auto text = source_.line_text(opening_line);
                const auto colon = source::index_of(text, u':');
                const auto open = source::index_of(text, u'[', colon + 1);
                std::int32_t end = opening_line.span.end();
                auto segments = parse_text_block_segments(opening_line, open, end);
                return Dialogue{
                    speaker,
                    std::move(segments),
                    source::TextSpan::from_bounds(opening_line.span.start, end)};
            }

            // 收集跨行正文，并在行与行之间补显式的换行片段，保持原始段落感。
            [[nodiscard]] std::vector<TextSegment> parse_text_block_segments(
                const source::SourceLine& opening_line,
                std::size_t open_bracket,
                std::int32_t& end)
            {
                const auto text = source_.line_text(opening_line);
                std::vector<TextSegment> segments;
                const auto close = source::index_of(text, u']', open_bracket + 1);

                if (close != npos) {
                    const auto content_start = open_bracket + 1;
                    auto parsed = parse_segments(
                        text.substr(content_start, close - content_start),
                        opening_line.span.start + static_cast<std::int32_t>(content_start),
                        true);
                    for (auto& segment : parsed) {
                        segments.push_back(std::move(segment));
                    }
                    end = opening_line.span.end();
                    advance();
                    return segments;
                }

                const auto first_start = open_bracket + 1;
                const auto first_raw = text.substr(first_start);
                const auto first_leading =
                    first_raw.size() - source::trim_start(first_raw).size();
                auto first_parsed = parse_segments(
                    source::trim(first_raw),
                    opening_line.span.start + static_cast<std::int32_t>(first_start + first_leading),
                    false);
                for (auto& segment : first_parsed) {
                    segments.push_back(std::move(segment));
                }
                end = opening_line.span.end();
                advance();

                bool closed = false;

                while (!is_at_end()) {
                    const auto line = *current();
                    const auto line_text = source_.line_text(line);
                    const auto close_index = source::index_of(line_text, u']');

                    if (close_index != npos) {
                        const auto raw = line_text.substr(0, close_index);
                        const auto leading =
                            raw.size() - source::trim_start(raw).size();
                        segments.push_back(TextSegment{
                            TextLiteral{u"\n", source::TextSpan::empty(line.span.start)}});
                        auto parsed = parse_segments(
                            source::trim(raw),
                            line.span.start + static_cast<std::int32_t>(leading),
                            false);
                        for (auto& segment : parsed) {
                            segments.push_back(std::move(segment));
                        }
                        end = line.span.start + static_cast<std::int32_t>(close_index) + 1;
                        closed = true;
                        advance();
                        break;
                    }

                    const auto leading =
                        line_text.size() - source::trim_start(line_text).size();
                    segments.push_back(TextSegment{
                        TextLiteral{u"\n", source::TextSpan::empty(line.span.start)}});
                    auto parsed = parse_segments(
                        source::trim(line_text),
                        line.span.start + static_cast<std::int32_t>(leading),
                        false);
                    for (auto& segment : parsed) {
                        segments.push_back(std::move(segment));
                    }
                    end = line.span.end();
                    advance();
                }

                if (!closed) {
                    error(
                        diagnostics::DiagnosticCode::UnterminatedTextBlock,
                        opening_line.span,
                        u"文本块缺少闭合的 ]。");
                    end = last_line_end();
                }

                trim_segments(segments);
                return segments;
            }

            // 把正文拆成 TextLiteral / TextReference；{{ 和 }} 是字面量大括号。
            [[nodiscard]] std::vector<TextSegment> parse_segments(
                std::u16string_view text,
                std::int32_t absolute_start,
                bool trim)
            {
                std::vector<TextSegment> segments;
                std::u16string literal;
                std::size_t literal_start = 0;
                std::size_t position = 0;

                auto flush = [&](std::size_t end_position) {
                    if (literal.empty()) {
                        return;
                    }

                    segments.push_back(TextSegment{
                        TextLiteral{
                            literal,
                            source::TextSpan::from_bounds(
                                absolute_start + static_cast<std::int32_t>(literal_start),
                                absolute_start + static_cast<std::int32_t>(end_position))}});
                    literal.clear();
                    literal_start = end_position;
                };

                while (position < text.size()) {
                    const auto current = text[position];

                    if (current == u'{') {
                        if (position + 1 < text.size() && text[position + 1] == u'{') {
                            literal.push_back(u'{');
                            position += 2;
                            continue;
                        }

                        flush(position);

                        const auto close = source::index_of(text, u'}', position + 1);
                        const auto newline = source::index_of(text, u'\n', position + 1);
                        if (close == npos || (newline != npos && newline < close)) {
                            error(
                                diagnostics::DiagnosticCode::UnterminatedReference,
                                source::TextSpan{
                                    absolute_start + static_cast<std::int32_t>(position), 1},
                                u"{ 引用没有在同一行闭合。");
                            literal_start = position;
                            literal.push_back(u'{');
                            ++position;
                            continue;
                        }

                        const auto raw = text.substr(position + 1, close - position - 1);
                        const auto name = source::trim(raw);
                        const auto leading =
                            raw.size() - source::trim_start(raw).size();

                        if (!is_identifier(name)) {
                            error(
                                diagnostics::DiagnosticCode::MalformedDirective,
                                source::TextSpan::from_bounds(
                                    absolute_start + static_cast<std::int32_t>(position),
                                    absolute_start + static_cast<std::int32_t>(close) + 1),
                                u"引用必须是合法的符号名，例如 {萧影}。");
                            segments.push_back(TextSegment{
                                TextLiteral{
                                    std::u16string(text.substr(
                                        position, close - position + 1)),
                                    source::TextSpan::from_bounds(
                                        absolute_start + static_cast<std::int32_t>(position),
                                        absolute_start + static_cast<std::int32_t>(close) + 1)}});
                        } else {
                            segments.push_back(TextSegment{
                                TextReference{
                                    std::u16string(name),
                                    source::TextSpan{
                                        absolute_start + static_cast<std::int32_t>(position + 1 + leading),
                                        static_cast<std::int32_t>(name.size())}}});
                        }

                        position = close + 1;
                        literal_start = position;
                        continue;
                    }

                    if (current == u'}') {
                        if (position + 1 < text.size() && text[position + 1] == u'}') {
                            literal.push_back(u'}');
                            position += 2;
                            continue;
                        }

                        literal.push_back(u'}');
                        ++position;
                        continue;
                    }

                    literal.push_back(current);
                    ++position;
                }

                flush(text.size());

                if (trim) {
                    trim_segments(segments);
                }

                return segments;
            }

            // 只裁掉首尾空白，并同步修正对应 TextLiteral 的 span。
            static void trim_segments(std::vector<TextSegment>& segments)
            {
                if (segments.empty()) {
                    return;
                }

                if (std::holds_alternative<TextLiteral>(segments.front())) {
                    auto& first = std::get<TextLiteral>(segments.front());
                    const auto trimmed = source::trim_start(first.text);
                    if (trimmed.empty()) {
                        segments.erase(segments.begin());
                    } else {
                        const auto removed = first.text.size() - trimmed.size();
                        first.text = std::u16string(trimmed);
                        first.span = source::TextSpan{
                            first.span.start + static_cast<std::int32_t>(removed),
                            std::max<std::int32_t>(
                                0, first.span.length - static_cast<std::int32_t>(removed))};
                    }
                }

                if (segments.empty()) {
                    return;
                }

                if (std::holds_alternative<TextLiteral>(segments.back())) {
                    auto& last = std::get<TextLiteral>(segments.back());
                    const auto trimmed = source::trim_end(last.text);
                    if (trimmed.empty()) {
                        segments.pop_back();
                    } else {
                        const auto removed = last.text.size() - trimmed.size();
                        last.text = std::u16string(trimmed);
                        last.span = source::TextSpan{
                            last.span.start,
                            std::max<std::int32_t>(
                                0, last.span.length - static_cast<std::int32_t>(removed))};
                    }
                }
            }

            // @fig "path"：路径字符串只做简单 " 和 \ 反转义。
            [[nodiscard]] std::optional<Figure> parse_figure()
            {
                const auto line = *current();
                advance();

                const auto trimmed = source::trim(source_.line_text(line));
                const auto rest = source::trim(trimmed.substr(5));
                if (rest.size() < 2 || rest.front() != u'"' || rest.back() != u'"') {
                    error(
                        diagnostics::DiagnosticCode::MalformedDirective,
                        line.span,
                        u"@fig 的语法是：@fig \"路径\"");
                    return std::nullopt;
                }

                return Figure{
                    unescape(rest.substr(1, rest.size() - 2)),
                    line.span};
            }

            // 出口块有两种写法：简写 @next -> 目标，或 @next(角色): 后跟多行分支。
            [[nodiscard]] Exit parse_exit()
            {
                const auto start_line = *current();
                const auto text = source_.line_text(start_line);
                const auto after_next = source::index_of(text, u"@next");
                auto cursor = after_next == npos ? 0 : after_next + 5;
                skip_whitespace(text, cursor);

                if (source::starts_with_at(text, cursor, u"->")) {
                    auto branch = parse_branch(start_line, cursor, true);
                    std::vector<ExitBranch> branches;
                    auto end = start_line.span.end();
                    if (branch) {
                        end = branch->span.end();
                        branches.push_back(std::move(*branch));
                    }

                    consume_after_shorthand();
                    validate_exit(std::nullopt, branches, start_line);

                    return Exit{
                        std::nullopt,
                        std::move(branches),
                        source::TextSpan::from_bounds(start_line.span.start, end)};
                }

                std::optional<std::u16string> character;
                if (cursor < text.size() && text[cursor] == u'(') {
                    const auto close = source::find_matching_paren(text, cursor);
                    if (!close) {
                        error(
                            diagnostics::DiagnosticCode::MalformedDirective,
                            start_line.span,
                            u"@next 的角色括号没有闭合。");
                        consume_to_next_module_or_end();
                        validate_exit(std::nullopt, {}, start_line);
                        return Exit{std::nullopt, {}, start_line.span};
                    }

                    const auto inner =
                        source::trim(text.substr(cursor + 1, *close - cursor - 1));
                    if (!inner.empty()) {
                        if (!is_identifier(inner)) {
                            error(
                                diagnostics::DiagnosticCode::MalformedDirective,
                                start_line.span,
                                u"@next 的角色名不合法。");
                        } else {
                            character = std::u16string(inner);
                        }
                    }

                    cursor = *close + 1;
                    skip_whitespace(text, cursor);
                }

                if (cursor >= text.size() || text[cursor] != u':') {
                    error(
                        diagnostics::DiagnosticCode::MalformedDirective,
                        start_line.span,
                        u"@next 的头部应为：@next(角色): 或 @next():");
                    auto recovered = parse_branch(start_line, cursor, false);
                    std::vector<ExitBranch> branches;
                    auto end = start_line.span.end();
                    if (recovered) {
                        end = recovered->span.end();
                        branches.push_back(std::move(*recovered));
                    }

                    consume_to_next_module_or_end();
                    validate_exit(character, branches, start_line);

                    return Exit{
                        std::move(character),
                        std::move(branches),
                        source::TextSpan::from_bounds(start_line.span.start, end)};
                }

                ++cursor;
                std::vector<ExitBranch> branches;
                std::optional<ExitBranch> first_branch;
                auto end = start_line.span.end();

                const auto inline_text =
                    source::trim(text.substr(cursor));
                if (!inline_text.empty()
                    && !source::starts_with(inline_text, u"//")) {
                    first_branch = parse_branch(start_line, cursor, false);
                } else {
                    advance();
                    skip_blank_and_comments();

                    if (!is_at_end()) {
                        const auto& first_line = *current();
                        const auto first_trimmed = current_trimmed();
                        if (!is_directive(first_trimmed, u"@module")
                            && !is_directive(first_trimmed, u"@title")) {
                            first_branch = parse_branch(first_line, 0, false);
                        }
                    }
                }

                if (first_branch) {
                    end = first_branch->span.end();
                    branches.push_back(std::move(*first_branch));
                }

                while (!is_at_end()) {
                    const auto line = *current();
                    const auto trimmed = current_trimmed();

                    if (is_directive(trimmed, u"@module")
                        || is_directive(trimmed, u"@title")) {
                        break;
                    }

                    if (trimmed.empty() || source::starts_with(trimmed, u"//")) {
                        advance();
                        continue;
                    }

                    if (!is_branch_start(trimmed)) {
                        error(
                            diagnostics::DiagnosticCode::ContentAfterExit,
                            line.span,
                            u"出口块之后不能再有内容。");
                        consume_to_next_module_or_end();
                        break;
                    }

                    auto branch = parse_branch(line, 0, false);
                    if (!branch) {
                        consume_to_next_module_or_end();
                        break;
                    }

                    end = branch->span.end();
                    branches.push_back(std::move(*branch));
                }

                validate_exit(character, branches, start_line);

                return Exit{
                    std::move(character),
                    std::move(branches),
                    source::TextSpan::from_bounds(start_line.span.start, end)};
            }

            // 一条分支：可选 (条件)、可选 [文案]、-> 目标、可选 { @set ... } 效果块。
            [[nodiscard]] std::optional<ExitBranch> parse_branch(
                const source::SourceLine& line,
                std::size_t content_start,
                bool shorthand)
            {
                const auto text = source_.line_text(line);
                auto position = content_start;
                skip_whitespace(text, position);
                const auto branch_start = position;

                if (position >= text.size()) {
                    error(
                        diagnostics::DiagnosticCode::MalformedExitBranch,
                        line.span,
                        u"出口分支不能为空。");
                    advance();
                    return std::nullopt;
                }

                std::optional<BoolExpr> condition;
                std::optional<source::TextSpan> condition_span;

                if (!shorthand && text[position] == u'(') {
                    const auto close = source::find_matching_paren(text, position);
                    if (!close) {
                        error(
                            diagnostics::DiagnosticCode::InvalidCondition,
                            line.span,
                            u"条件括号没有闭合。");
                        advance();
                        return std::nullopt;
                    }

                    const auto inner_start = position + 1;
                    const auto inner = text.substr(inner_start, *close - inner_start);
                    condition_span = source::TextSpan::from_bounds(
                        line.span.start + static_cast<std::int32_t>(position),
                        line.span.start + static_cast<std::int32_t>(*close) + 1);

                    if (!source::trim(inner).empty()) {
                        condition = parse_condition(
                            inner,
                            line.span.start + static_cast<std::int32_t>(inner_start),
                            diagnostics_);
                    }

                    position = *close + 1;
                    skip_whitespace(text, position);
                }

                std::optional<std::u16string> label;
                std::optional<source::TextSpan> label_span;

                if (!shorthand && position < text.size() && text[position] == u'[') {
                    const auto close = source::index_of(text, u']', position + 1);
                    if (close == npos) {
                        error(
                            diagnostics::DiagnosticCode::MalformedExitBranch,
                            line.span,
                            u"选项文案缺少右方括号。");
                        advance();
                        return std::nullopt;
                    }

                    const auto raw = text.substr(position + 1, close - position - 1);
                    const auto leading =
                        raw.size() - source::trim_start(raw).size();
                    const auto trimmed_label = source::trim(raw);
                    label = std::u16string(trimmed_label);
                    label_span = trimmed_label.empty()
                        ? source::TextSpan::empty(
                            line.span.start + static_cast<std::int32_t>(position) + 1)
                        : source::TextSpan{
                            line.span.start
                                + static_cast<std::int32_t>(position + 1 + leading),
                            static_cast<std::int32_t>(trimmed_label.size())};

                    position = close + 1;
                    skip_whitespace(text, position);
                }

                if (!source::starts_with_at(text, position, u"->")) {
                    error(
                        diagnostics::DiagnosticCode::MalformedExitBranch,
                        line.span,
                        u"出口分支缺少 ->。");
                    advance();
                    return std::nullopt;
                }

                position += 2;
                skip_whitespace(text, position);

                auto target_end = source::index_of(text, u'{', position);
                if (target_end == npos) {
                    target_end = text.size();
                }

                const auto raw_target = text.substr(position, target_end - position);
                const auto target_leading =
                    raw_target.size() - source::trim_start(raw_target).size();
                const auto target = source::trim(raw_target);

                if (target.empty()) {
                    error(
                        diagnostics::DiagnosticCode::MalformedExitBranch,
                        line.span,
                        u"出口分支缺少目标模块。");
                    advance();
                    return std::nullopt;
                }

                const auto target_span = source::TextSpan{
                    line.span.start
                        + static_cast<std::int32_t>(position + target_leading),
                    static_cast<std::int32_t>(target.size())};
                position = target_end;

                std::vector<Effect> effects;
                std::optional<source::TextSpan> effect_span;
                bool advance_at_end = true;

                if (position < text.size() && text[position] == u'{') {
                    const auto effect_start = position;
                    const auto close = source::index_of(text, u'}', position + 1);

                    if (close != npos) {
                        parse_effect_statements(
                            text, position + 1, close, line, effects);
                        effect_span = source::TextSpan::from_bounds(
                            line.span.start + static_cast<std::int32_t>(effect_start),
                            line.span.start + static_cast<std::int32_t>(close) + 1);

                        if (!source::trim(text.substr(close + 1)).empty()) {
                            error(
                                diagnostics::DiagnosticCode::MalformedExitBranch,
                                line.span,
                                u"效果块之后不能再有内容。");
                        }
                    } else {
                        if (!source::trim(text.substr(position + 1)).empty()) {
                            error(
                                diagnostics::DiagnosticCode::MalformedExitBranch,
                                line.span,
                                u"多行效果块的 { 后不能有内容。");
                        }

                        advance();
                        advance_at_end = false;

                        auto effect_end = line.span.end();
                        bool closed = false;

                        while (!is_at_end()) {
                            const auto effect_line = *current();
                            const auto effect_text = source_.line_text(effect_line);
                            const auto trimmed = source::trim(effect_text);

                            if (trimmed == u"}") {
                                closed = true;
                                effect_end = effect_line.span.end();
                                advance();
                                break;
                            }

                            if (trimmed.empty()
                                || source::starts_with(trimmed, u"//")) {
                                advance();
                                continue;
                            }

                            parse_effect_statements(
                                effect_text, 0, effect_text.size(),
                                effect_line, effects);
                            effect_end = effect_line.span.end();
                            advance();
                        }

                        if (!closed) {
                            error(
                                diagnostics::DiagnosticCode::MalformedExitBranch,
                                line.span,
                                u"效果块缺少闭合的 }。");
                        }

                        effect_span = source::TextSpan::from_bounds(
                            line.span.start + static_cast<std::int32_t>(effect_start),
                            effect_end);
                    }
                }

                if (advance_at_end) {
                    advance();
                }

                const auto branch_end = effect_span ? effect_span->end() : line.span.end();
                const auto branch_span = source::TextSpan::from_bounds(
                    line.span.start + static_cast<std::int32_t>(branch_start),
                    branch_end);

                return ExitBranch{
                    std::move(condition),
                    std::move(label),
                    std::u16string(target),
                    std::move(effects),
                    branch_span,
                    target_span,
                    condition_span,
                    label_span,
                    effect_span,
                };
            }

            // 效果块内按 ';' 分割多条 @set；同一块内重复赋值给 warning。
            void parse_effect_statements(
                std::u16string_view text,
                std::size_t start,
                std::size_t end,
                const source::SourceLine& line,
                std::vector<Effect>& effects)
            {
                std::size_t position = start;

                while (position < end) {
                    while (position < end
                        && (is_space(text[position]) || text[position] == u';')) {
                        ++position;
                    }

                    if (position >= end) {
                        break;
                    }

                    const auto statement_start = position;
                    const auto next_semicolon = source::index_of(text, u';', position);
                    const auto statement_end =
                        next_semicolon != npos && next_semicolon < end
                        ? next_semicolon
                        : end;

                    const auto raw =
                        text.substr(statement_start, statement_end - statement_start);
                    const auto leading =
                        raw.size() - source::trim_start(raw).size();
                    const auto statement = source::trim(raw);
                    const auto absolute_start =
                        line.span.start
                        + static_cast<std::int32_t>(statement_start + leading);

                    const bool is_set = source::starts_with(statement, u"@set")
                        && (statement.size() == 4 || is_space(statement[4]));

                    if (!is_set) {
                        error(
                            diagnostics::DiagnosticCode::MalformedEffect,
                            line.span,
                            u"效果块内只允许 @set 语句。");
                    } else {
                        const auto rest = source::trim(statement.substr(4));
                        const auto parts = source::split_whitespace(rest);

                        bool value = false;
                        if (parts.size() != 2
                            || !is_identifier(parts[0])
                            || !try_parse_boolean(parts[1], value)) {
                            error(
                                diagnostics::DiagnosticCode::MalformedEffect,
                                line.span,
                                u"@set 的语法是：@set <布尔变量名> true|false");
                        } else {
                            const auto name = std::u16string(parts[0]);
                            Effect effect{
                                name,
                                value,
                                source::TextSpan{
                                    absolute_start,
                                    static_cast<std::int32_t>(statement.size())}};

                            if (std::any_of(
                                    effects.begin(), effects.end(),
                                    [&](const Effect& existing) {
                                        return existing.name == name;
                                    })) {
                                std::u16string message =
                                    u"同一效果块内重复赋值：";
                                message += name;
                                warning(
                                    diagnostics::DiagnosticCode::DuplicateEffectAssignment,
                                    effect.span,
                                    std::move(message));
                            }

                            effects.push_back(std::move(effect));
                        }
                    }

                    position = statement_end + 1;
                }
            }

            // 出口语义校验：至少一条分支、必须有兜底、选择/自动模式不能混用。
            void validate_exit(
                const std::optional<std::u16string>& character,
                const std::vector<ExitBranch>& branches,
                const source::SourceLine& start_line)
            {
                if (branches.empty()) {
                    error(
                        diagnostics::DiagnosticCode::MalformedExitBranch,
                        start_line.span,
                        u"出口块至少需要一条分支。");
                    return;
                }

                if (std::none_of(
                        branches.begin(), branches.end(),
                        [](const ExitBranch& branch) {
                            return !branch.condition.has_value();
                        })) {
                    error(
                        diagnostics::DiagnosticCode::MissingFallback,
                        start_line.span,
                        u"出口块缺少无条件兜底分支。");
                }

                const bool choice_mode = character.has_value();
                const bool has_label = std::any_of(
                    branches.begin(), branches.end(),
                    [](const ExitBranch& branch) {
                        return branch.label_span.has_value();
                    });
                const bool has_unlabeled = std::any_of(
                    branches.begin(), branches.end(),
                    [](const ExitBranch& branch) {
                        return !branch.label_span.has_value();
                    });

                if (choice_mode) {
                    for (const auto& branch : branches) {
                        if (!branch.label_span.has_value()
                            || !branch.label.has_value()
                            || branch.label->empty()) {
                            error(
                                diagnostics::DiagnosticCode::InvalidBranchLabel,
                                branch.span,
                                u"选择模式的分支必须带非空 [文案]。");
                        }
                    }
                } else {
                    for (const auto& branch : branches) {
                        if (branch.label_span.has_value()) {
                            error(
                                diagnostics::DiagnosticCode::InvalidBranchLabel,
                                branch.span,
                                u"自动模式的分支不能带 [文案]。");
                        }
                    }
                }

                if (has_label && has_unlabeled) {
                    error(
                        diagnostics::DiagnosticCode::ModeMismatch,
                        start_line.span,
                        u"出口块不能混用带文案和不带文案的分支。");
                }
            }

            // 简写出口只能有一条分支；跳过后续内容并只报告一次错误。
            void consume_after_shorthand()
            {
                bool reported = false;

                while (!is_at_end()) {
                    const auto line = *current();
                    const auto trimmed = current_trimmed();

                    if (is_directive(trimmed, u"@module")
                        || is_directive(trimmed, u"@title")) {
                        break;
                    }

                    if (!reported && !trimmed.empty()
                        && !source::starts_with(trimmed, u"//")) {
                        error(
                            diagnostics::DiagnosticCode::ContentAfterExit,
                            line.span,
                            u"简写出口只允许一条分支；后续内容无效。");
                        reported = true;
                    }

                    advance();
                }
            }

            // 错误恢复：丢弃当前出口块的剩余内容，直到下一个模块或文件结束。
            void consume_to_next_module_or_end()
            {
                while (!is_at_end()) {
                    const auto trimmed = current_trimmed();
                    if (is_directive(trimmed, u"@module")
                        || is_directive(trimmed, u"@title")) {
                        break;
                    }
                    advance();
                }
            }

            // 解析 "@var name true|false" 的 name/value 部分。
            [[nodiscard]] bool try_parse_assignment(
                std::u16string_view trimmed,
                std::u16string_view keyword,
                const source::SourceLine& line,
                std::u16string& name,
                bool& value)
            {
                name.clear();
                value = false;

                const auto rest = source::trim(trimmed.substr(keyword.size()));
                const auto parts = source::split_whitespace(rest);

                if (parts.size() != 2
                    || !is_identifier(parts[0])
                    || !try_parse_boolean(parts[1], value)) {
                    std::u16string message(keyword);
                    message += u" 的语法是：";
                    message += keyword;
                    message += u" <布尔变量名> true|false";
                    error(
                        diagnostics::DiagnosticCode::MalformedDirective,
                        line.span,
                        std::move(message));
                    return false;
                }

                name = std::u16string(parts[0]);
                return true;
            }

            // 解析 "@char name" 这类单名称声明。
            [[nodiscard]] bool try_parse_single_name(
                std::u16string_view trimmed,
                std::u16string_view keyword,
                const source::SourceLine& line,
                std::u16string& name)
            {
                const auto rest = source::trim(trimmed.substr(keyword.size()));

                if (!is_identifier(rest)) {
                    std::u16string message(keyword);
                    message += u" 的语法是：";
                    message += keyword;
                    message += u" <名称>";
                    error(
                        diagnostics::DiagnosticCode::MalformedDirective,
                        line.span,
                        std::move(message));
                    name.clear();
                    return false;
                }

                name = std::u16string(rest);
                return true;
            }

            // 判断一行是否以 "角色:[" 开头；否则按旁白/未知行处理。
            [[nodiscard]] static bool try_get_dialogue_prefix(
                std::u16string_view text,
                std::u16string& speaker,
                std::u16string_view& rest)
            {
                speaker.clear();
                rest = {};

                const auto colon = source::index_of(text, u':');
                if (colon == npos || colon == 0) {
                    return false;
                }

                const auto candidate = source::trim(text.substr(0, colon));
                if (!is_identifier(candidate)) {
                    return false;
                }

                const auto after = source::trim_start(text.substr(colon + 1));
                if (!source::starts_with(after, u"[")) {
                    return false;
                }

                speaker = std::u16string(candidate);
                rest = after;
                return true;
            }

            // 图片路径只需处理 " 和 \，其余反斜杠保持原样。
            [[nodiscard]] static std::u16string unescape(std::u16string_view text)
            {
                std::u16string result;
                result.reserve(text.size());

                for (std::size_t index = 0; index < text.size(); ++index) {
                    const auto current = text[index];
                    if (current == u'\\' && index + 1 < text.size()) {
                        const auto next = text[index + 1];
                        if (next == u'"' || next == u'\\') {
                            result.push_back(next);
                            ++index;
                            continue;
                        }
                    }
                    result.push_back(current);
                }

                return result;
            }

            const source::SourceText& source_;
            diagnostics::DiagnosticBag& diagnostics_;
            std::size_t index_{0};
            std::set<std::u16string> module_titles_;
        };

    } // namespace

    // 统一入口：先按扩展名/首行判断 .wstory，再选择对应解析路径。
    ScriptFile parse(
        const source::SourceText& source,
        diagnostics::DiagnosticBag& diagnostics)
    {
        WeftParser parser(source, diagnostics);
        if (parser.is_wstory_file()) {
            return parser.parse_wstory();
        }
        return parser.parse_weft();
    }

} // namespace weftline::syntax
