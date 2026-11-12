#include <gtest/gtest.h>

#include <algorithm>
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

    [[nodiscard]] bool has_diagnostic(
        const weftline::projects::ProjectCompilationResult& result,
        std::string_view code)
    {
        return std::any_of(
            result.diagnostics.begin(), result.diagnostics.end(),
            [code](const weftline::projects::ProjectDiagnostic& item) {
                return item.diagnostic.code == code;
            });
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
        EXPECT_EQ(result.graph.value()->modules.size(), 1u);
        EXPECT_EQ(result.projection.value()->modules.size(), 1u);
    }

    TEST(ProjectCompiler, BuildsRuntimeOnlyWhenRequested)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@var a false
@module A
    [你好]
@next -> END { @set a true }
)");

        CompilationOptions options;
        options.build_runtime = true;
        const auto result = weftline::projects::compile(snapshot, options);

        EXPECT_FALSE(result.has_errors());
        ASSERT_TRUE(result.runtime.has_value());
        ASSERT_EQ(result.runtime.value()->modules.size(), 1u);
        EXPECT_EQ(result.runtime.value()->modules[0].id, u"A");
        EXPECT_FALSE(result.projection.has_value());
    }

    TEST(ProjectCompiler, OmitsRuntimeWhenNotRequested)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> END
)");

        const auto result = weftline::projects::compile(snapshot);

        EXPECT_FALSE(result.has_errors());
        EXPECT_FALSE(result.runtime.has_value());
    }

    TEST(ProjectCompiler, StrictModeDropsGraphOnErrors)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> Missing
)");

        CompilationOptions options;
        options.build_runtime = true;
        const auto strict = weftline::projects::compile(snapshot, options);

        EXPECT_TRUE(strict.has_errors());
        EXPECT_FALSE(strict.graph.has_value());
        EXPECT_FALSE(strict.analysis.has_value());
        EXPECT_FALSE(strict.projection.has_value());
        EXPECT_FALSE(strict.runtime.has_value());
    }

    TEST(ProjectCompiler, PartialErrorsDropRuntime)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> Missing
)");

        CompilationOptions options;
        options.allow_partial = true;
        options.build_runtime = true;
        const auto partial = weftline::projects::compile(snapshot, options);

        EXPECT_TRUE(partial.has_errors());
        ASSERT_TRUE(partial.graph.has_value());
        EXPECT_FALSE(partial.runtime.has_value());
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

    TEST(ProjectCompiler, RebindsChangedFileWithoutDeclarationChanges)
    {
        ProjectSnapshot snapshot;
        snapshot.folder = "test";
        snapshot.documents["chapter.weft"] = R"(@title 第一章
@char 小明
@module A
    [{小明}。]
@next -> END
)";

        weftline::projects::CompilationCache cache;
        const auto first = weftline::projects::compile(snapshot, {}, &cache);
        EXPECT_FALSE(has_diagnostic(first, "WFT2014"));

        snapshot.documents["chapter.weft"] = R"(@title 第一章
@char 小明
@module A
    [{小红}。]
@next -> END
)";

        const auto second = weftline::projects::compile(snapshot, {}, &cache);
        EXPECT_TRUE(has_diagnostic(second, "WFT2014"));
    }

    TEST(ProjectCompiler, RebindsAllFilesWhenDeclarationsChange)
    {
        ProjectSnapshot snapshot;
        snapshot.folder = "test";
        snapshot.documents["a.weft"] = R"(@title 第一章
@char 小明
@module A
@next -> B
)";
        snapshot.documents["b.weft"] = R"(@title 第二章
@module B
    [{小明}。]
@next -> END
)";

        weftline::projects::CompilationCache cache;
        const auto first = weftline::projects::compile(snapshot, {}, &cache);
        EXPECT_FALSE(has_diagnostic(first, "WFT2014"));

        snapshot.documents["a.weft"] = R"(@title 第一章
@module A
@next -> B
)";

        const auto second = weftline::projects::compile(snapshot, {}, &cache);
        EXPECT_TRUE(has_diagnostic(second, "WFT2014"));
    }

    TEST(ProjectCompiler, ReusesGraphWhenOnlyWStoryChanges)
    {
        ProjectSnapshot snapshot;
        snapshot.folder = "test";
        snapshot.documents["chapter.weft"] = R"(@title 第一章
@module A
@next -> END
)";
        snapshot.documents["story.wstory"] = R"(@project 测试
    @chapters
        @chapter 第一章 [旧描述。]
)";

        CompilationOptions options;
        options.build_projection = true;
        weftline::projects::CompilationCache cache;

        const auto first = weftline::projects::compile(snapshot, options, &cache);
        ASSERT_TRUE(first.graph.has_value());
        ASSERT_TRUE(first.projection.has_value());
        const auto first_graph = first.graph.value();
        const auto first_projection = first.projection.value();

        snapshot.documents["story.wstory"] = R"(@project 测试
    @chapters
        @chapter 第一章 [新描述。]
)";

        const auto second = weftline::projects::compile(snapshot, options, &cache);
        ASSERT_TRUE(second.graph.has_value());
        ASSERT_TRUE(second.projection.has_value());
        EXPECT_EQ(second.graph.value().get(), first_graph.get());
        EXPECT_EQ(second.projection.value().get(), first_projection.get());
        ASSERT_EQ(second.model.chapters.size(), 1u);
        ASSERT_TRUE(second.model.chapters[0].description.has_value());
        EXPECT_EQ(*second.model.chapters[0].description, u"新描述。");
    }

    TEST(ProjectCompiler, ReusesGraphAndBuildsProjectionOnDemand)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> END
)");
        weftline::projects::CompilationCache cache;

        const auto first = weftline::projects::compile(snapshot, {}, &cache);
        ASSERT_TRUE(first.graph.has_value());
        ASSERT_FALSE(first.projection.has_value());
        const auto first_graph = first.graph.value();

        CompilationOptions options;
        options.build_projection = true;
        const auto second = weftline::projects::compile(snapshot, options, &cache);

        ASSERT_TRUE(second.graph.has_value());
        ASSERT_TRUE(second.projection.has_value());
        EXPECT_EQ(second.graph.value().get(), first_graph.get());
    }

    TEST(ProjectCompiler, InvalidatesGraphWhenWeftChanges)
    {
        const auto snapshot = make_snapshot(R"(@title 第一章
@module A
@next -> END
)");
        weftline::projects::CompilationCache cache;

        const auto first = weftline::projects::compile(snapshot, {}, &cache);
        ASSERT_TRUE(first.graph.has_value());
        const auto first_graph = first.graph.value();

        auto changed = snapshot;
        changed.documents["chapter.weft"] = R"(@title 第一章
@module A
    [新正文。]
@next -> END
)";

        const auto second = weftline::projects::compile(changed, {}, &cache);
        ASSERT_TRUE(second.graph.has_value());
        EXPECT_NE(second.graph.value().get(), first_graph.get());
    }

} // namespace
