#pragma once

#include <compare>
#include <cstdint>
#include <string_view>

#include "weftline/source/source_text.hpp"

namespace weftline::syntax {

    /**
     * @brief 词法单元种类。
     */
    enum class TokenKind : std::uint8_t {
        Bad,
        EndOfFile,
        Whitespace,
        NewLine,
        Comment,
        AtKeyword,
        Identifier,
        BoolLiteral,
        Number,
        StringLiteral,
        Colon,
        Arrow,
        OpenBracket,
        CloseBracket,
        OpenParen,
        CloseParen,
        OpenBrace,
        CloseBrace,
        Semicolon,
        Comma,
    };

    /**
     * @brief 返回稳定的 camelCase 名称，用于 JSON 输出。
     */
    [[nodiscard]]
    constexpr std::u16string_view token_kind_name(TokenKind kind) noexcept
    {
        switch (kind) {
            using enum TokenKind;
            case Bad:
                return u"bad";
            case EndOfFile:
                return u"endOfFile";
            case Whitespace:
                return u"whitespace";
            case NewLine:
                return u"newLine";
            case Comment:
                return u"comment";
            case AtKeyword:
                return u"atKeyword";
            case Identifier:
                return u"identifier";
            case BoolLiteral:
                return u"boolLiteral";
            case Number:
                return u"number";
            case StringLiteral:
                return u"stringLiteral";
            case Colon:
                return u"colon";
            case Arrow:
                return u"arrow";
            case OpenBracket:
                return u"openBracket";
            case CloseBracket:
                return u"closeBracket";
            case OpenParen:
                return u"openParen";
            case CloseParen:
                return u"closeParen";
            case OpenBrace:
                return u"openBrace";
            case CloseBrace:
                return u"closeBrace";
            case Semicolon:
                return u"semicolon";
            case Comma:
                return u"comma";
        }

        return u"bad";
    }

    /**
     * @brief 一个词法单元：种类 + 源码范围。
     *
     * token 本身不拥有文本；需要文本时用 SourceText::get_text(span) 获取。
     */
    struct Token
    {
        TokenKind kind{TokenKind::Bad};
        source::TextSpan span;

        auto operator<=>(const Token&) const = default;
    };

} // namespace weftline::syntax
