#include <gtest/gtest.h>

#include <cstddef>

#include "weftline/source/source_text.hpp"
#include "weftline/syntax/lexer.hpp"

namespace {

    using weftline::source::SourceText;
    using weftline::syntax::Lexer;
    using weftline::syntax::LexResult;
    using weftline::syntax::TokenKind;

    /// @brief 统计某种 token 的出现次数。
    std::size_t count_kind(const LexResult& result, TokenKind kind)
    {
        std::size_t count = 0;
        for (const auto& token : result.tokens) {
            if (token.kind == kind) {
                ++count;
            }
        }
        return count;
    }

} // namespace

// 指令与中文标识符应被正确切分。
TEST(Lexer, RecognizesDirectivesAndChineseIdentifiers)
{
    const auto source = SourceText::from_utf8("@title 雨夜\n@module 相信\n");
    const auto result = Lexer(source).tokenize();

    EXPECT_EQ(count_kind(result, TokenKind::AtKeyword), 2u);
    EXPECT_EQ(count_kind(result, TokenKind::Identifier), 2u);
    EXPECT_EQ(count_kind(result, TokenKind::NewLine), 2u);
    EXPECT_TRUE(result.diagnostics.empty());
}

// 未闭合字符串应产生一条诊断。
TEST(Lexer, ReportsUnterminatedString)
{
    const auto source = SourceText::from_utf8("x \"unterminated");
    const auto result = Lexer(source).tokenize();

    ASSERT_FALSE(result.diagnostics.empty());
    EXPECT_EQ(result.diagnostics.front().code, "WFT1010");
}

// start/length 使用 UTF-16 code unit，行列位置应正确。
TEST(SourceText, ReportsLinePositionsInUtf16Offset)
{
    const auto source = SourceText::from_utf8("@title 雨夜\n@module 相信\n");

    EXPECT_EQ(source.length(), 21);
    EXPECT_EQ(source.lines().size(), 3u);

    const auto position = source.get_line_position(7);
    EXPECT_EQ(position.line, 1);
    EXPECT_EQ(position.character, 8);
}
