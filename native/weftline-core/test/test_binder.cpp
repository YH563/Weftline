#include <gtest/gtest.h>

#include <algorithm>
#include <memory>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include "weftline/binding/binder.hpp"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    using weftline::diagnostics::DiagnosticBag;
    using weftline::projects::ProjectFile;
    using weftline::syntax::WStoryFile;
    using weftline::syntax::WeftFile;

    [[nodiscard]] bool has_code(const DiagnosticBag& diagnostics, std::string_view code)
    {
        return std::any_of(
            diagnostics.begin(), diagnostics.end(),
            [code](const auto& diagnostic) { return diagnostic.code == code; });
    }

    [[nodiscard]] bool has_project_code(
        const std::vector<weftline::projects::ProjectDiagnostic>& diagnostics,
        std::string_view code)
    {
        return std::any_of(
            diagnostics.begin(), diagnostics.end(),
            [code](const auto& item) { return item.diagnostic.code == code; });
    }

    [[nodiscard]] WeftFile parse_weft(
        std::string_view text, std::string_view path, DiagnosticBag& diagnostics)
    {
        const auto source =
            weftline::source::SourceText::from_utf8(text, std::string(path));
        return std::get<WeftFile>(weftline::syntax::parse(source, diagnostics));
    }

    [[nodiscard]] WStoryFile parse_wstory(
        std::string_view text, std::string_view path, DiagnosticBag& diagnostics)
    {
        const auto source =
            weftline::source::SourceText::from_utf8(text, std::string(path));
        return std::get<WStoryFile>(weftline::syntax::parse(source, diagnostics));
    }

    [[nodiscard]] ProjectFile project_file(std::string_view path, std::string_view text)
    {
        DiagnosticBag diagnostics;
        auto ast = parse_weft(text, path, diagnostics);
        EXPECT_TRUE(diagnostics.empty());
        return ProjectFile{
            std::string(path),
            std::make_shared<const WeftFile>(std::move(ast))};
    }

} // namespace

TEST(Binder, ResolvesDeclaredSymbols)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第28章
@vars
    @var a false

@chars
    @char 萧影

@items
    @item 钥匙

@scenes
    @scene 医院

@module 测试
    @scene 医院
    [{萧影}拿着{钥匙}。]
    萧影:[你好。]
@next(萧影):(a)[继续] -> END { @set a true }
    ()[结束] -> END
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(diagnostics.empty());
}

TEST(Binder, ReportsUndeclaredCharacterInDialogue)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第29章
@module 测试
    萧影:[你好。]
@next -> END
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::UndeclaredCharacter));
}

TEST(Binder, ReportsUndeclaredExitCharacter)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第30章
@module 测试
@next(萧影):[继续] -> END
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::UndeclaredCharacter));
}

TEST(Binder, ReportsUndeclaredTextReference)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第31章
@chars
    @char 萧影

@module 测试
    [{钥匙}。]
@next -> END
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::UndeclaredReference));
}

TEST(Binder, ReportsUndeclaredConditionVariable)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第32章
@module 测试
@next():(missing) -> END
    () -> END
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(has_code(
        diagnostics,
        weftline::diagnostics::DiagnosticCode::UndeclaredConditionVariable));
}

TEST(Binder, ReportsUndeclaredVariableInEffect)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第33章
@module 测试
@next(萧影):[继续] -> END { @set missing true }
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::UndeclaredVariable));
}

TEST(Binder, ReportsDuplicateWeftDeclarations)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第34章
@vars
    @var a false
    @var a true

@chars
    @char 萧影
    @char 萧影

@items
    @item 钥匙
    @item 钥匙

@scenes
    @scene 医院
    @scene 医院
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::DuplicateGlobalVariable));
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::DuplicateCharacter));
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::DuplicateItem));
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::DuplicateScene));
}

TEST(Binder, ReportsCrossKindDuplicateSymbol)
{
    DiagnosticBag diagnostics;
    const auto file = parse_weft(R"(@title 第35章
@chars
    @char 同名

@items
    @item 同名
)", "test.weft", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "test.weft");
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::DuplicateSymbol));
}

TEST(Binder, ReportsDuplicateWStoryDeclarations)
{
    DiagnosticBag diagnostics;
    const auto file = parse_wstory(R"(@project 雨夜

@chars
    @char 萧影
    @char 萧影
)", "project.wstory", diagnostics);
    ASSERT_TRUE(diagnostics.empty());

    weftline::binding::bind(file, diagnostics, "project.wstory");
    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::DuplicateCharacter));
}

TEST(ProjectBinder, ResolvesDeclarationsAcrossFiles)
{
    auto declarations = project_file("decl.weft", R"(@title 第40章
@vars
    @var 信任 false

@chars
    @char 萧影

@scenes
    @scene 医院
)");
    auto story = project_file("story.weft", R"(@title 第41章
@module 测试
    @scene 医院
    [{萧影}来了。]
    萧影:[你好。]
@next(萧影):(信任)[继续] -> END
    ()[结束] -> END
)");

    std::vector<ProjectFile> files;
    files.push_back(std::move(declarations));
    files.push_back(std::move(story));
    const auto result = weftline::projects::bind(std::move(files), nullptr, {});

    EXPECT_TRUE(result.diagnostics.empty());
    EXPECT_NE(result.index.symbols.find(u"萧影"), result.index.symbols.end());
    EXPECT_NE(result.index.symbols.find(u"信任"), result.index.symbols.end());
    EXPECT_NE(result.index.modules.find(u"测试"), result.index.modules.end());
}

TEST(ProjectBinder, ReportsMissingReferenceAcrossFiles)
{
    auto story = project_file("story.weft", R"(@title 第42章
@module 测试
    [{钥匙}。]
@next -> END
)");

    std::vector<ProjectFile> files;
    files.push_back(std::move(story));
    const auto result = weftline::projects::bind(std::move(files), nullptr, {});

    EXPECT_TRUE(has_project_code(
        result.diagnostics, weftline::diagnostics::DiagnosticCode::UndeclaredReference));
}

TEST(ProjectBinder, ReportsDuplicateDeclarationAcrossFiles)
{
    auto first = project_file("a.weft", R"(@title 第43章
@vars
    @var a false
)");
    auto second = project_file("b.weft", R"(@title 第44章
@vars
    @var a true
)");

    std::vector<ProjectFile> files;
    files.push_back(std::move(first));
    files.push_back(std::move(second));
    const auto result = weftline::projects::bind(std::move(files), nullptr, {});

    const auto found = std::find_if(
        result.diagnostics.begin(), result.diagnostics.end(),
        [](const auto& item) {
            return item.diagnostic.code
                == weftline::diagnostics::DiagnosticCode::DuplicateGlobalVariable;
        });
    ASSERT_NE(found, result.diagnostics.end());
    EXPECT_EQ(found->file_path, "b.weft");
    ASSERT_TRUE(found->diagnostic.related.has_value());
    EXPECT_EQ(found->diagnostic.related->file_path, "a.weft");
}

TEST(ProjectBinder, ReportsDuplicateModuleTitleAcrossFiles)
{
    auto first = project_file("a.weft", R"(@title 第45章
@module 同名
@next -> END
)");
    auto second = project_file("b.weft", R"(@title 第46章
@module 同名
@next -> END
)");

    std::vector<ProjectFile> files;
    files.push_back(std::move(first));
    files.push_back(std::move(second));
    const auto result = weftline::projects::bind(std::move(files), nullptr, {});

    const auto found = std::find_if(
        result.diagnostics.begin(), result.diagnostics.end(),
        [](const auto& item) {
            return item.diagnostic.code
                == weftline::diagnostics::DiagnosticCode::DuplicateModuleTitle;
        });
    ASSERT_NE(found, result.diagnostics.end());
    EXPECT_EQ(found->file_path, "b.weft");
    ASSERT_TRUE(found->diagnostic.related.has_value());
    EXPECT_EQ(found->diagnostic.related->file_path, "a.weft");
}

TEST(ProjectBinder, MapsModulesToChapters)
{
    auto first = project_file("a.weft", R"(@title 第一章
@module A
@next -> END
)");
    auto second = project_file("b.weft", R"(@title 第二章
@module B
@next -> END
)");

    std::vector<ProjectFile> files;
    files.push_back(std::move(first));
    files.push_back(std::move(second));
    const auto result = weftline::projects::bind(std::move(files), nullptr, {});

    EXPECT_TRUE(result.diagnostics.empty());
    const auto a = result.index.modules.find(u"A");
    const auto b = result.index.modules.find(u"B");
    ASSERT_NE(a, result.index.modules.end());
    ASSERT_NE(b, result.index.modules.end());
    EXPECT_EQ(a->second.chapter, u"第一章");
    EXPECT_EQ(b->second.chapter, u"第二章");
    EXPECT_NE(result.index.chapters.find(u"第一章"), result.index.chapters.end());
    EXPECT_NE(result.index.chapters.find(u"第二章"), result.index.chapters.end());
}

TEST(ProjectBinder, ReportsDuplicateChapterTitleAcrossFiles)
{
    auto first = project_file("a.weft", R"(@title 同名
@module A
@next -> END
)");
    auto second = project_file("b.weft", R"(@title 同名
@module B
@next -> END
)");

    std::vector<ProjectFile> files;
    files.push_back(std::move(first));
    files.push_back(std::move(second));
    const auto result = weftline::projects::bind(std::move(files), nullptr, {});

    const auto found = std::find_if(
        result.diagnostics.begin(), result.diagnostics.end(),
        [](const auto& item) {
            return item.diagnostic.code
                == weftline::diagnostics::DiagnosticCode::DuplicateChapterTitle;
        });
    ASSERT_NE(found, result.diagnostics.end());
    EXPECT_EQ(found->file_path, "b.weft");
    ASSERT_TRUE(found->diagnostic.related.has_value());
    EXPECT_EQ(found->diagnostic.related->file_path, "a.weft");
}
