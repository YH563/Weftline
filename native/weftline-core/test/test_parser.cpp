#include <gtest/gtest.h>

#include <fstream>
#include <sstream>
#include <string>
#include <variant>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/ast_printer.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    std::string read_file(const std::string& path)
    {
        std::ifstream file(path, std::ios::binary);
        if (!file) {
            return {};
        }

        std::ostringstream buffer;
        buffer << file.rdbuf();
        return buffer.str();
    }

    const char* const expected_believe_ast = R"(WeftFile
    vars
        var trust_low = false
        var has_key = false
    chars
        char 萧影
        char 影子
    items
        item 医院钥匙
    scenes
        scene 街道
        scene 医院
    Module "序" scene=街道
        [雨夜，{萧影}走向医院。]
        @next auto
            -> 相信 { trust_low = true }
    Module "相信" scene=医院
        [雨水模糊了视线，{萧影}在泥泞中奔跑。]
        萧影:[你来了。]
        影子:[别回头。]
        @fig "assets/hospital.png"
        @next choice (萧影)
            [相信他] when trust_low -> 医院 { has_key = true }
            [沉默] -> 沉默
    Module "医院" scene=医院
        [两人走进医院。]
        @next auto
            when has_key -> 地下层
            -> 走廊
    Module "地下层" scene=医院
        [萧影找到了{医院钥匙}。]
        @next auto
            -> END
    Module "走廊" scene=医院
        [走廊空无一人。]
        @next auto
            -> END
    Module "沉默" scene=医院
        [萧影没有说话。]
        @next auto
            -> END
)";

} // namespace

TEST(Parser, ParsesMinimalWeftFile)
{
    const auto source = weftline::source::SourceText::from_utf8(
        "@title T\n@module M\n[x]\n@next -> END\n");
    weftline::diagnostics::DiagnosticBag diagnostics;

    const auto script = weftline::syntax::parse(source, diagnostics);
    EXPECT_FALSE(diagnostics.has_errors());

    const auto* file = std::get_if<weftline::syntax::WeftFile>(&script);
    ASSERT_NE(file, nullptr);
    EXPECT_EQ(file->title, u"T");
    ASSERT_EQ(file->modules.size(), 1u);
    EXPECT_EQ(file->modules[0].title, u"M");
    ASSERT_TRUE(file->modules[0].exit.has_value());
    ASSERT_EQ(file->modules[0].exit->branches.size(), 1u);
    EXPECT_EQ(file->modules[0].exit->branches[0].target, u"END");
}

TEST(Parser, AllowsFirstBranchOnNextLine)
{
    const auto source = weftline::source::SourceText::from_utf8(
        "@title T\n"
        "@vars\n"
        "    @var a false\n"
        "@module M\n"
        "    [x]\n"
        "@next():\n"
        "    (a) -> END\n"
        "    () -> END\n");
    weftline::diagnostics::DiagnosticBag diagnostics;

    const auto script = weftline::syntax::parse(source, diagnostics);
    EXPECT_TRUE(diagnostics.empty());

    const auto* file = std::get_if<weftline::syntax::WeftFile>(&script);
    ASSERT_NE(file, nullptr);
    ASSERT_EQ(file->modules.size(), 1u);
    ASSERT_TRUE(file->modules[0].exit.has_value());
    ASSERT_EQ(file->modules[0].exit->branches.size(), 2u);
    ASSERT_TRUE(file->modules[0].exit->branches[0].condition.has_value());
    EXPECT_EQ(file->modules[0].exit->branches[0].target, u"END");
    EXPECT_FALSE(file->modules[0].exit->branches[1].condition.has_value());
}

TEST(Parser, MatchesCSharpAstForBelieveSample)
{
    const std::string path = std::string(WEFTLINE_SAMPLES_DIR) + "/believe.weft";
    const std::string text = read_file(path);
    ASSERT_FALSE(text.empty()) << "cannot read " << path;

    const auto source = weftline::source::SourceText::from_utf8(text, path);
    weftline::diagnostics::DiagnosticBag diagnostics;
    const auto script = weftline::syntax::parse(source, diagnostics);

    EXPECT_FALSE(diagnostics.has_errors());
    EXPECT_EQ(weftline::syntax::print_ast_utf8(script), expected_believe_ast);
}
