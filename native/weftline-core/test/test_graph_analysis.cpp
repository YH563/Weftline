#include <gtest/gtest.h>

#include <algorithm>
#include <memory>
#include <string>
#include <string_view>
#include <unordered_set>
#include <variant>
#include <vector>

#include "weftline/ir/graph_analysis_builder.hpp"
#include "weftline/ir/project_model_builder.hpp"
#include "weftline/ir/story_graph_builder.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    using weftline::ir::ChapterEdge;
    using weftline::ir::GraphAnalysis;
    using weftline::ir::ProjectModel;
    using weftline::ir::StoryGraph;
    using weftline::projects::LoadedProject;
    using weftline::projects::ProjectFile;
    using weftline::syntax::WeftFile;

    [[nodiscard]] ProjectFile parse_file(
        std::string_view path, std::string_view source)
    {
        weftline::diagnostics::DiagnosticBag diagnostics;
        const auto text = weftline::source::SourceText::from_utf8(
            source, std::string(path));
        auto script = weftline::syntax::parse(text, diagnostics);
        EXPECT_TRUE(diagnostics.empty());
        return ProjectFile{
            std::string(path),
            std::make_shared<const WeftFile>(
                std::get<WeftFile>(std::move(script))),
        };
    }

    struct AnalysisResult
    {
        ProjectModel model;
        StoryGraph graph;
        GraphAnalysis analysis;
    };

    [[nodiscard]] AnalysisResult build_analysis(std::vector<ProjectFile> files)
    {
        LoadedProject project;
        project.folder = "test";
        project.weft_files = files;

        const auto binding =
            weftline::projects::bind(project.weft_files, nullptr, {});
        EXPECT_TRUE(binding.diagnostics.empty());

        auto model = weftline::ir::build_project_model(project, binding.index);
        auto graph = weftline::ir::build_story_graph(project, model);
        auto analysis = weftline::ir::build_graph_analysis(model, graph);

        return AnalysisResult{
            std::move(model),
            std::move(graph),
            std::move(analysis),
        };
    }

    [[nodiscard]] bool contains(
        const std::unordered_set<std::u16string>& values,
        std::u16string_view value)
    {
        return values.find(std::u16string(value)) != values.end();
    }

    [[nodiscard]] const ChapterEdge* find_chapter_edge(
        const GraphAnalysis& analysis,
        std::u16string_view from,
        std::u16string_view to)
    {
        for (const auto& edge : analysis.chapter_edges) {
            if (edge.from_chapter == from && edge.to_chapter == to) {
                return &edge;
            }
        }
        return nullptr;
    }

    TEST(GraphAnalysis, BuildsReachabilityReferencesPathsAndChapterEdges)
    {
        auto first = parse_file("chapter1.weft", R"(@title 第一章
@vars
    @var x false

@chars
    @char 小明

@items
    @item 钥匙

@scenes
    @scene 房间

@module A
    @scene 房间
    [小明拿着{钥匙}在房间。]
    小明:[你好。]
@next():(x) -> B
    () -> B

@module B
    [B。]
@next():(x) -> C
    () -> C
    () -> D
)");

        auto second = parse_file("chapter2.weft", R"(@title 第二章
@module C
    [C。]
@next -> END

@module D
    [D。]
@next -> END
)");

        std::vector<ProjectFile> files;
        files.push_back(std::move(first));
        files.push_back(std::move(second));
        const auto result = build_analysis(std::move(files));

        for (const auto* id : {u"A", u"B", u"C", u"D"}) {
            EXPECT_TRUE(contains(result.analysis.reachable, id));
            EXPECT_TRUE(contains(result.analysis.can_reach_end, id));
        }

        EXPECT_EQ(
            result.analysis.references.characters.at(u"小明"),
            (std::vector<std::u16string>{u"A"}));
        EXPECT_EQ(
            result.analysis.references.items.at(u"钥匙"),
            (std::vector<std::u16string>{u"A"}));
        EXPECT_EQ(
            result.analysis.references.scenes.at(u"房间"),
            (std::vector<std::u16string>{u"A"}));
        EXPECT_EQ(
            result.analysis.references.variables.at(u"x"),
            (std::vector<std::u16string>{u"A", u"B"}));

        const auto* chapter_edge = find_chapter_edge(
            result.analysis, u"第一章", u"第二章");
        ASSERT_NE(chapter_edge, nullptr);
        EXPECT_EQ(chapter_edge->count(), 3u);

        std::vector<std::u16string> targets;
        for (const auto& detail : chapter_edge->details) {
            targets.push_back(detail.to_module);
            EXPECT_EQ(detail.mode, weftline::ir::ExitMode::Auto);
        }
        EXPECT_EQ(targets, (std::vector<std::u16string>{u"C", u"C", u"D"}));

        const auto* end_edge = find_chapter_edge(
            result.analysis, u"第二章", result.graph.end);
        ASSERT_NE(end_edge, nullptr);
        EXPECT_EQ(end_edge->count(), 2u);

        const auto paths =
            result.analysis.find_paths(u"A", result.graph.end);
        ASSERT_EQ(paths.size(), 2u);
        for (const auto& path : paths) {
            EXPECT_EQ(path.front(), u"A");
            EXPECT_EQ(path.back(), u"END");
        }
    }

    TEST(GraphAnalysis, ReportsUnreachableAndCannotReachEndSets)
    {
        auto file = parse_file("chapter.weft", R"(@title 第一章
@module A
@next -> END

@module B
@next -> B
)");

        std::vector<ProjectFile> files;
        files.push_back(std::move(file));
        const auto result = build_analysis(std::move(files));

        EXPECT_TRUE(contains(result.analysis.reachable, u"A"));
        EXPECT_FALSE(contains(result.analysis.reachable, u"B"));
        EXPECT_TRUE(contains(result.analysis.can_reach_end, u"A"));
        EXPECT_FALSE(contains(result.analysis.can_reach_end, u"B"));
    }

} // namespace
