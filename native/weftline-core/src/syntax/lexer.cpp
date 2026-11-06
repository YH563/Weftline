#include "weftline/syntax/lexer.hpp"

#include <string_view>

namespace weftline::syntax {
    namespace {

        /// @brief 判断标识符是否为 true/false。
        [[nodiscard]]
        bool is_bool_literal(std::u16string_view text) noexcept
        {
            return text == u"true" || text == u"false";
        }

        /// @brief 词法层的水平空白（不含换行）。
        [[nodiscard]]
        constexpr bool is_horizontal_space(char16_t value) noexcept
        {
            return value == u' ' || value == u'\t';
        }

    } // namespace

    LexResult Lexer::tokenize() const
    {
        LexResult result;

        const auto& text = source_.text();
        const auto length = static_cast<std::int32_t>(text.size());

        std::int32_t index = 0;
        while (index < length) {
            const auto start = index;
            const auto current = text[static_cast<std::size_t>(index)];

            // 连续空白合并成一个 token。
            if (is_horizontal_space(current)) {
                while (index < length && is_horizontal_space(text[static_cast<std::size_t>(index)])) {
                    ++index;
                }
                result.tokens.push_back({TokenKind::Whitespace, {start, index - start}});
                continue;
            }

            // \r\n 作为一个换行 token。
            if (current == u'\r' || current == u'\n') {
                ++index;
                if (current == u'\r' && index < length
                    && text[static_cast<std::size_t>(index)] == u'\n') {
                    ++index;
                }
                result.tokens.push_back({TokenKind::NewLine, {start, index - start}});
                continue;
            }

            // 行注释：到行尾为止，不包含换行。
            if (current == u'/' && index + 1 < length
                && text[static_cast<std::size_t>(index + 1)] == u'/') {
                index += 2;
                while (index < length) {
                    const auto value = text[static_cast<std::size_t>(index)];
                    if (value == u'\r' || value == u'\n') {
                        break;
                    }
                    ++index;
                }
                result.tokens.push_back({TokenKind::Comment, {start, index - start}});
                continue;
            }

            // @ 指令：把 @name 整体作为一个 token。
            if (current == u'@') {
                ++index;
                while (index < length && is_identifier_part(text[static_cast<std::size_t>(index)])) {
                    ++index;
                }
                result.tokens.push_back({TokenKind::AtKeyword, {start, index - start}});
                continue;
            }

            // 标识符；true/false 单独归类为布尔字面量。
            if (is_identifier_start(current)) {
                ++index;
                while (index < length && is_identifier_part(text[static_cast<std::size_t>(index)])) {
                    ++index;
                }

                const auto word = std::u16string_view(text).substr(
                    static_cast<std::size_t>(start), static_cast<std::size_t>(index - start));
                result.tokens.push_back(
                    {is_bool_literal(word) ? TokenKind::BoolLiteral : TokenKind::Identifier,
                     {start, index - start}});
                continue;
            }

            // 简单十进制数字串。
            if (is_ascii_digit(current)) {
                ++index;
                while (index < length && is_ascii_digit(text[static_cast<std::size_t>(index)])) {
                    ++index;
                }
                result.tokens.push_back({TokenKind::Number, {start, index - start}});
                continue;
            }

            // 双引号字符串；反斜杠转义下一个字符。
            if (current == u'"') {
                ++index;
                bool terminated = false;

                while (index < length) {
                    const auto value = text[static_cast<std::size_t>(index)];
                    if (value == u'\\') {
                        index += index + 1 < length ? 2 : 1;
                        continue;
                    }
                    ++index;
                    if (value == u'"') {
                        terminated = true;
                        break;
                    }
                }

                result.tokens.push_back({TokenKind::StringLiteral, {start, index - start}});
                // 未闭合字符串：仍产出 token，同时记录诊断。
                if (!terminated) {
                    result.diagnostics.push_back(
                        diagnostics::make_unterminated_string({start, index - start}));
                }
                continue;
            }

            // -> 出口箭头。
            if (current == u'-' && index + 1 < length
                && text[static_cast<std::size_t>(index + 1)] == u'>') {
                index += 2;
                result.tokens.push_back({TokenKind::Arrow, {start, index - start}});
                continue;
            }

            // 单字符标点。
            auto kind = TokenKind::Bad;
            switch (current) {
                using enum TokenKind;
                case u':':
                    kind = Colon;
                    ++index;
                    break;
                case u'[':
                    kind = OpenBracket;
                    ++index;
                    break;
                case u']':
                    kind = CloseBracket;
                    ++index;
                    break;
                case u'(':
                    kind = OpenParen;
                    ++index;
                    break;
                case u')':
                    kind = CloseParen;
                    ++index;
                    break;
                case u'{':
                    kind = OpenBrace;
                    ++index;
                    break;
                case u'}':
                    kind = CloseBrace;
                    ++index;
                    break;
                case u';':
                    kind = Semicolon;
                    ++index;
                    break;
                case u',':
                    kind = Comma;
                    ++index;
                    break;
                default:
                    break;
            }

            // 未识别字符：记录诊断，但仍产出一个 Bad token。
            if (kind == TokenKind::Bad) {
                ++index;
                result.diagnostics.push_back(
                    diagnostics::make_unexpected_character(current, {start, 1}));
            }

            result.tokens.push_back({kind, {start, index - start}});
        }

        // EOF 哨兵，方便上层遍历。
        result.tokens.push_back({TokenKind::EndOfFile, {length, 0}});
        return result;
    }

} // namespace weftline::syntax
