#include <gtest/gtest.h>

#include <string>
#include <string_view>

#include "weftline/projects/compilation_cache.hpp"
#include "weftline/projects/project_compiler.hpp"
#include "weftline/projects/project_snapshot.hpp"

namespace {

    using weftline::projects::CompilationOptions;
    using weftline::projects::ProjectSnapshot;

    [[nodiscard]] ProjectSnapshot make_snapshot(std::string_view source)
    {
        ProjectSnapshot snapshot;
        snapshot.folder = "test";
        snapshot.documents["chapter.weft"] = std::string(source);
        return snapshot;
    }

    TEST(ProjectCompiler, BuildsGraphAnalysisAndProjection)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> END
)");

        CompilationOptions options;
        options.build_projection = true;
        const auto result = weftline::projects::compile(snapshot, options);

        EXPECT_FALSE(result.has_errors());
        EXPECT_TRUE(result.diagnostics.empty());
        ASSERT_EQ(result.model.chapters.size(), 1u);
        ASSERT_TRUE(result.graph.has_value());
        ASSERT_TRUE(result.analysis.has_value());
        ASSERT_TRUE(result.projection.has_value());
        EXPECT_EQ(result.graph->modules.size(), 1u);
        EXPECT_EQ(result.projection->modules.size(), 1u);
    }

    TEST(ProjectCompiler, StrictModeDropsGraphOnErrors)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> Missing
)");

        const auto strict = weftline::projects::compile(snapshot);

        EXPECT_TRUE(strict.has_errors());
        EXPECT_FALSE(strict.graph.has_value());
        EXPECT_FALSE(strict.analysis.has_value());
        EXPECT_FALSE(strict.projection.has_value());
    }

    TEST(ProjectCompiler, PartialModeKeepsGraphOnErrors)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> Missing
)");

        CompilationOptions options;
        options.allow_partial = true;
        options.build_projection = true;
        const auto partial = weftline::projects::compile(snapshot, options);

        EXPECT_TRUE(partial.has_errors());
        EXPECT_FALSE(partial.diagnostics.empty());
        ASSERT_TRUE(partial.graph.has_value());
        ASSERT_TRUE(partial.analysis.has_value());
        ASSERT_TRUE(partial.projection.has_value());
    }

    TEST(ProjectCompiler, ReusesParsedAstWhenTextIsUnchanged)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> END
)");
        weftline::projects::CompilationCache cache;

        const auto first = weftline::projects::compile(snapshot, {}, &cache);
        ASSERT_EQ(first.project.weft_files.size(), 1u);
        const auto first_ast = first.project.weft_files[0].ast;

        const auto second = weftline::projects::compile(snapshot, {}, &cache);
        ASSERT_EQ(second.project.weft_files.size(), 1u);
        EXPECT_EQ(second.project.weft_files[0].ast.get(), first_ast.get());
    }

    TEST(ProjectCompiler, InvalidatesCacheWhenTextChanges)
    {
        auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> END
)");
        weftline::projects::CompilationCache cache;

        const auto first = weftline::projects::compile(snapshot, {}, &cache);
        ASSERT_EQ(first.project.weft_files.size(), 1u);
        const auto first_ast = first.project.weft_files[0].ast;

        snapshot.documents["chapter.weft"] = R"(@title 第二章
@module B
@next -> END
)";

        const auto second = weftline::projects::compile(snapshot, {}, &cache);
        ASSERT_EQ(second.project.weft_files.size(), 1u);
        EXPECT_NE(second.project.weft_files[0].ast.get(), first_ast.get());
        ASSERT_EQ(second.model.chapters.size(), 1u);
        EXPECT_EQ(second.model.chapters[0].title, u"第二章");
    }

} // namespace
