#include <gtest/gtest.h>

#include <algorithm>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/generation/wstory_generator.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    using weftline::diagnostics::DiagnosticBag;
    using weftline::source::SourceText;
    using weftline::syntax::WStoryFile;
    using weftline::syntax::WeftFile;

    [[nodiscard]] bool has_code(const DiagnosticBag& diagnostics, std::string_view code)
    {
        return std::any_of(
            diagnostics.begin(), diagnostics.end(),
            [code](const auto& diagnostic) { return diagnostic.code == code; });
    }

    [[nodiscard]] WStoryFile parse_wstory(
        std::string_view text, DiagnosticBag& diagnostics)
    {
        const auto source = SourceText::from_utf8(text, "project.wstory");
        return std::get<WStoryFile>(weftline::syntax::parse(source, diagnostics));
    }

    [[nodiscard]] WeftFile parse_weft(
        std::string_view text, DiagnosticBag& diagnostics)
    {
        const auto source = SourceText::from_utf8(text, "chapter.weft");
        return std::get<WeftFile>(weftline::syntax::parse(source, diagnostics));
    }

} // namespace

TEST(WStoryParser, ParsesProjectSummaryDocsAndGroups)
{
    const auto source = R"(@project 雨夜
    @summary [一个关于信任的故事。]

    @docs
        @doc 世界规则 [医院在雨夜不关门。]

    @chars
        @char 萧影 [主角。]
        @char 影子 [萧影的旧识。]

    @items
        @item 医院钥匙 [医院地下室的钥匙。]

    @scenes
        @scene 医院 [雨夜里的医院。]

    @vars
        @var 信任 false [萧影是否信任影子。]
        @var 有钥匙 false [是否拿到了医院钥匙。]
)";
    DiagnosticBag diagnostics;
    const auto file = parse_wstory(source, diagnostics);

    EXPECT_TRUE(diagnostics.empty());
    EXPECT_EQ(file.project_name, u"雨夜");
    ASSERT_TRUE(file.summary.has_value());
    EXPECT_EQ(file.summary->text, u"一个关于信任的故事。");
    ASSERT_EQ(file.docs.size(), 1u);
    EXPECT_EQ(file.docs[0].title, u"世界规则");
    EXPECT_EQ(file.docs[0].body.text, u"医院在雨夜不关门。");
    ASSERT_EQ(file.characters.size(), 2u);
    ASSERT_TRUE(file.characters[0].description.has_value());
    EXPECT_EQ(file.characters[0].description->text, u"主角。");
    EXPECT_EQ(file.items.size(), 1u);
    EXPECT_EQ(file.scenes.size(), 1u);
    ASSERT_EQ(file.variables.size(), 2u);
    EXPECT_FALSE(file.variables[0].value);
    ASSERT_TRUE(file.variables[0].description.has_value());
    EXPECT_EQ(file.variables[0].description->text, u"萧影是否信任影子。");
}

TEST(WStoryParser, ParsesChapters)
{
    const auto source = R"(@project 雨夜
    @chapters
        @chapter 第一章 雨夜 [开篇。]
        @chapter 第二章 病房 [转折。]
)";
    DiagnosticBag diagnostics;
    const auto file = parse_wstory(source, diagnostics);

    EXPECT_TRUE(diagnostics.empty());
    ASSERT_EQ(file.chapters.size(), 2u);
    EXPECT_EQ(file.chapters[0].title, u"第一章 雨夜");
    ASSERT_TRUE(file.chapters[0].description.has_value());
    EXPECT_EQ(file.chapters[0].description->text, u"开篇。");
}

TEST(WStoryParser, ParsesMultilineDescriptionWithAttachedBrackets)
{
    const auto source = R"(@project 雨夜

@chars
    @char 萧影 [主角。
    也是叙述者。]
)";
    DiagnosticBag diagnostics;
    const auto file = parse_wstory(source, diagnostics);

    EXPECT_TRUE(diagnostics.empty());
    ASSERT_EQ(file.characters.size(), 1u);
    ASSERT_TRUE(file.characters[0].description.has_value());
    EXPECT_EQ(file.characters[0].description->text, u"主角。\n也是叙述者。");
}

TEST(WStoryParser, ReportsMissingProject)
{
    DiagnosticBag diagnostics;
    static_cast<void>(parse_wstory(R"(@chars
    @char 萧影
)", diagnostics));

    EXPECT_TRUE(has_code(diagnostics, weftline::diagnostics::DiagnosticCode::MalformedDirective));
}

TEST(WStoryParser, ReportsVariableWithoutValue)
{
    DiagnosticBag diagnostics;
    static_cast<void>(parse_wstory(R"(@project 雨夜

@vars
    @var 信任 [没有初始值。]
)", diagnostics));

    EXPECT_TRUE(has_code(diagnostics, weftline::diagnostics::DiagnosticCode::MalformedDirective));
}

TEST(WStoryParser, ReportsSetInWStory)
{
    DiagnosticBag diagnostics;
    static_cast<void>(parse_wstory(R"(@project 雨夜

@vars
    @var 信任 false

@set 信任 true
)", diagnostics));

    EXPECT_TRUE(has_code(diagnostics, weftline::diagnostics::DiagnosticCode::SetInProjectFile));
}

TEST(WStoryGenerator, GeneratesFromWeftAndPreservesDescriptions)
{
    DiagnosticBag weft_diagnostics;
    auto weft = parse_weft(R"(@title 第49章
@vars
    @var trust_low false
    @var has_key false

@chars
    @char 萧影
    @char 影子

@items
    @item 医院钥匙

@scenes
    @scene 医院

@module 相信
    [正文。]
@next -> END
)", weft_diagnostics);
    ASSERT_TRUE(weft_diagnostics.empty());

    DiagnosticBag existing_diagnostics;
    auto existing = parse_wstory(R"(@project 雨夜
    @summary [一个关于信任的故事。]

    @chapters
        @chapter 第49章 [章节描述。]

    @chars
        @char 萧影 [主角。]

    @vars
        @var trust_low false [萧影是否信任影子。]
)", existing_diagnostics);
    ASSERT_TRUE(existing_diagnostics.empty());

    std::vector<WeftFile> files;
    files.push_back(std::move(weft));
    const auto generated = weftline::source::utf16_to_utf8(
        weftline::generation::generate(files, &existing, u"fallback"));

    EXPECT_NE(generated.find("@project 雨夜"), std::string::npos);
    EXPECT_NE(generated.find("@chapter 第49章 [章节描述。]"), std::string::npos);
    EXPECT_NE(generated.find("@char 萧影 [主角。]"), std::string::npos);
    EXPECT_NE(generated.find("@char 影子 []"), std::string::npos);
    EXPECT_NE(generated.find("@var trust_low false [萧影是否信任影子。]"), std::string::npos);
    EXPECT_NE(generated.find("@var has_key false []"), std::string::npos);

    DiagnosticBag reparsed_diagnostics;
    const auto reparsed_source =
        SourceText::from_utf8(generated, "project.wstory");
    const auto reparsed = weftline::syntax::parse(reparsed_source, reparsed_diagnostics);
    EXPECT_TRUE(reparsed_diagnostics.empty());
    EXPECT_NE(std::get_if<WStoryFile>(&reparsed), nullptr);
}

TEST(WStoryGenerator, GenerationIsIdempotent)
{
    DiagnosticBag weft_diagnostics;
    auto weft = parse_weft(R"(@title 第50章
@vars
    @var a false

@chars
    @char 萧影
)", weft_diagnostics);
    ASSERT_TRUE(weft_diagnostics.empty());

    DiagnosticBag existing_diagnostics;
    auto existing = parse_wstory(R"(@project 雨夜
    @chars
        @char 萧影 [主角。]
)", existing_diagnostics);
    ASSERT_TRUE(existing_diagnostics.empty());

    std::vector<WeftFile> files;
    files.push_back(std::move(weft));

    const auto first = weftline::generation::generate(files, &existing, u"fallback");

    DiagnosticBag reparsed_diagnostics;
    const auto reparsed_source = SourceText::from_utf8(
        weftline::source::utf16_to_utf8(first), "project.wstory");
    auto reparsed = std::get<WStoryFile>(
        weftline::syntax::parse(reparsed_source, reparsed_diagnostics));
    ASSERT_TRUE(reparsed_diagnostics.empty());

    const auto second = weftline::generation::generate(files, &reparsed, u"fallback");
    EXPECT_EQ(first, second);
}

TEST(WStoryGenerator, PreservesSummaryAndDocs)
{
    DiagnosticBag weft_diagnostics;
    auto weft = parse_weft(R"(@title 第51章
@chars
    @char 萧影
)", weft_diagnostics);
    ASSERT_TRUE(weft_diagnostics.empty());

    DiagnosticBag existing_diagnostics;
    auto existing = parse_wstory(R"(@project 雨夜
    @summary [一个故事。]

    @docs
        @doc 世界规则 [医院不关门。]
)", existing_diagnostics);
    ASSERT_TRUE(existing_diagnostics.empty());

    std::vector<WeftFile> files;
    files.push_back(std::move(weft));
    const auto generated = weftline::source::utf16_to_utf8(
        weftline::generation::generate(files, &existing, u"fallback"));

    EXPECT_NE(generated.find("@summary [一个故事。]"), std::string::npos);
    EXPECT_NE(generated.find("@doc 世界规则 [医院不关门。]"), std::string::npos);
}
