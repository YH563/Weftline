#include <gtest/gtest.h>

#include <algorithm>
#include <memory>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/projects/validator.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    using weftline::diagnostics::DiagnosticBag;
    using weftline::projects::LoadedProject;
    using weftline::projects::ProjectDiagnostic;
    using weftline::projects::ProjectFile;
    using weftline::projects::ProjectIndex;
    using weftline::projects::ProjectSnapshot;
    using weftline::syntax::WeftFile;

    struct Compiled
    {
        LoadedProject project;
        ProjectIndex index;
        std::vector<ProjectDiagnostic> diagnostics;
    };

    [[nodiscard]] bool has_code(
        const std::vector<ProjectDiagnostic>& diagnostics, std::string_view code)
    {
        return std::any_of(
            diagnostics.begin(), diagnostics.end(),
            [code](const auto& item) { return item.diagnostic.code == code; });
    }

    [[nodiscard]] Compiled compile_single(std::string_view source)
    {
        DiagnosticBag parse_diagnostics;
        const auto source_text =
            weftline::source::SourceText::from_utf8(source, "test.weft");
        auto script = weftline::syntax::parse(source_text, parse_diagnostics);
        EXPECT_TRUE(parse_diagnostics.empty());

        LoadedProject project;
        project.folder = "test";
        project.weft_files.push_back(ProjectFile{
            "test.weft",
            std::make_shared<const WeftFile>(
                std::get<WeftFile>(std::move(script))),
        });

        auto binding = weftline::projects::bind(project.weft_files, nullptr, {});
        auto diagnostics = weftline::projects::validate(project, binding.index);

        return Compiled{
            std::move(project),
            std::move(binding.index),
            std::move(diagnostics),
        };
    }

} // namespace

TEST(ProjectLoader, LoadsDocumentsAndWStory)
{
    ProjectSnapshot snapshot;
    snapshot.folder = "mem";
    snapshot.version = 1;
    snapshot.documents["chapter.weft"] =
        "@title 第一章\n@module A\n@next -> END\n";
    snapshot.documents["project.wstory"] =
        "@project 雨夜\n";

    const auto project = weftline::projects::load(snapshot);

    EXPECT_TRUE(project.diagnostics.empty());
    ASSERT_EQ(project.weft_files.size(), 1u);
    EXPECT_EQ(project.weft_files[0].file_path, "chapter.weft");
    ASSERT_TRUE(project.wstory.has_value());
    EXPECT_EQ(project.wstory->project_name, u"雨夜");
    EXPECT_EQ(project.wstory_path, "project.wstory");
}

TEST(Validator, AcceptsValidGraph)
{
    const auto result = compile_single(R"(@title 第36章
@module A
@next -> END
)");

    EXPECT_TRUE(result.diagnostics.empty());
}

TEST(Validator, ReportsMissingTarget)
{
    const auto result = compile_single(R"(@title 第37章
@module A
@next -> Missing
)");

    EXPECT_TRUE(has_code(
        result.diagnostics, weftline::diagnostics::DiagnosticCode::TargetNotFound));
}

TEST(Validator, ReportsUnreachableModule)
{
    const auto result = compile_single(R"(@title 第38章
@module A
@next -> END

@module B
@next -> END
)");

    EXPECT_TRUE(std::any_of(
        result.diagnostics.begin(), result.diagnostics.end(),
        [](const auto& item) {
            return item.diagnostic.code
                    == weftline::diagnostics::DiagnosticCode::UnreachableModule
                && item.diagnostic.message.find(u"B") != std::u16string::npos;
        }));
}

TEST(Validator, ReportsPathCannotReachEnd)
{
    const auto result = compile_single(R"(@title 第39章
@module A
@next -> B

@module B
@next -> B
)");

    EXPECT_TRUE(has_code(
        result.diagnostics,
        weftline::diagnostics::DiagnosticCode::PathCannotReachEnd));
}

TEST(Validator, ReportsEmptyProject)
{
    LoadedProject project;
    project.folder = "test";
    ProjectIndex index;

    const auto diagnostics = weftline::projects::validate(project, index);

    EXPECT_TRUE(has_code(
        diagnostics, weftline::diagnostics::DiagnosticCode::EmptyProject));
}
