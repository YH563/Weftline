#include <gtest/gtest.h>

#include <memory>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include "weftline/graph/builders.hpp"
#include "weftline/ir/builders.hpp"
#include "weftline/runtime/builders.hpp"
#include "weftline/serialization/ir_json.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

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

    TEST(IrJson, SerializesStoryGraph)
    {
        auto file = parse_file("chapter.weft", R"(@title T
@vars
    @var a false
@module M
    @scene 街道
    [hello]
@next():(a) -> END { @set a true }
    () -> END
)");

        std::vector<ProjectFile> files;
        files.push_back(std::move(file));

        LoadedProject project;
        project.folder = "test";
        project.weft_files = files;

        const auto binding =
            weftline::projects::bind(project.weft_files, nullptr, {});
        ASSERT_TRUE(binding.diagnostics.empty());

        const auto model =
            weftline::ir::build_project_model(project, binding.index);
        const auto graph = weftline::ir::build_story_graph(project, model);
        const auto json = weftline::serialization::story_graph_to_json(graph);

        EXPECT_NE(json.find("\"schemaVersion\":2"), std::string::npos);
        EXPECT_NE(json.find("\"end\":\"END\""), std::string::npos);
        EXPECT_NE(json.find("\"entry\":\"M\""), std::string::npos);
        EXPECT_NE(
            json.find("\"scenes\":[{\"name\":\"街道\",\"contentIndex\":0}]"),
            std::string::npos);
        EXPECT_NE(json.find("\"globals\":[{\"name\":\"a\",\"initial\":false}]"),
                  std::string::npos);
        EXPECT_NE(json.find("\"id\":\"M\""), std::string::npos);
        EXPECT_NE(json.find("\"kind\":\"narration\""), std::string::npos);
        EXPECT_NE(json.find("\"segments\":[{\"kind\":\"text\",\"text\":\"hello\"}]"),
                  std::string::npos);
        EXPECT_NE(json.find("\"mode\":\"auto\""), std::string::npos);
        EXPECT_NE(json.find("\"condition\":{\"kind\":\"ref\",\"name\":\"a\"}"),
                  std::string::npos);
        EXPECT_NE(json.find("\"effects\":[{\"name\":\"a\",\"value\":true}]"),
                  std::string::npos);

        // StoryGraph 载荷必须带源码位置；播放器载荷不带（见 test_runtime_projection）。
        EXPECT_NE(json.find("\"source\":{\"filePath\""), std::string::npos);
    }

    TEST(IrJson, SerializesEntryCandidates)
    {
        auto file = parse_file("chapter.weft", R"(@title T
@module A
@next -> END

@module B
@next -> END
)");

        std::vector<ProjectFile> files;
        files.push_back(std::move(file));

        LoadedProject project;
        project.folder = "test";
        project.weft_files = files;

        const auto binding =
            weftline::projects::bind(project.weft_files, nullptr, {});
        ASSERT_TRUE(binding.diagnostics.empty());

        const auto model =
            weftline::ir::build_project_model(project, binding.index);
        const auto graph = weftline::ir::build_story_graph(project, model);
        const auto graph_json = weftline::serialization::story_graph_to_json(graph);

        EXPECT_NE(
            graph_json.find("\"entryCandidates\":[\"B\"]"),
            std::string::npos);

        const auto projection =
            weftline::graph::build_graph_projection(model, graph);
        const auto projection_json =
            weftline::serialization::graph_projection_to_json(projection);

        EXPECT_NE(
            projection_json.find("\"entryCandidates\":[\"B\"]"),
            std::string::npos);
        EXPECT_NE(
            projection_json.find(
                "\"flags\":[\"entryCandidate\",\"targetsEnd\"]"),
            std::string::npos);
    }

    TEST(IrJson, SerializesGraphProjection)
    {
        auto file = parse_file("chapter.weft", R"(@title T
@vars
    @var a false
@module M
    [hello]
@next():(a) -> END { @set a true }
    () -> END
)");

        std::vector<ProjectFile> files;
        files.push_back(std::move(file));

        LoadedProject project;
        project.folder = "test";
        project.weft_files = files;

        const auto binding =
            weftline::projects::bind(project.weft_files, nullptr, {});
        ASSERT_TRUE(binding.diagnostics.empty());

        const auto model =
            weftline::ir::build_project_model(project, binding.index);
        const auto graph = weftline::ir::build_story_graph(project, model);
        const auto projection =
            weftline::graph::build_graph_projection(model, graph);
        const auto json = weftline::serialization::graph_projection_to_json(projection);

        EXPECT_NE(json.find("\"entryModuleId\":\"M\""), std::string::npos);
        EXPECT_NE(json.find("\"chapterId\":\"T\""), std::string::npos);
        EXPECT_NE(json.find("\"moduleIds\":[\"M\"]"), std::string::npos);
        EXPECT_NE(json.find("\"flags\":[\"entry\",\"hasEndEdge\"]"),
                  std::string::npos);
        EXPECT_NE(json.find("\"flags\":[\"entry\",\"targetsEnd\"]"),
                  std::string::npos);
        EXPECT_NE(json.find("\"branchCount\":2"), std::string::npos);
        EXPECT_NE(json.find("\"firstLine\":\"hello\""), std::string::npos);
        EXPECT_NE(json.find("\"moduleEdges\":["), std::string::npos);
        EXPECT_NE(json.find("\"chapterEdges\":["), std::string::npos);
        EXPECT_NE(json.find("\"references\":{"), std::string::npos);
        EXPECT_NE(json.find("\"variables\":{\"a\":[\"M\"]}"),
                  std::string::npos);
    }

    TEST(IrJson, SerializesReferencesInStableOrder)
    {
        // unordered_map 的遍历序不可依赖：输出必须按键排序，两次序列化完全一致。
        weftline::graph::GraphProjection projection;
        projection.references.variables.emplace(
            u"zeta", std::vector<std::u16string>{u"M2"});
        projection.references.variables.emplace(
            u"alpha", std::vector<std::u16string>{u"M1"});
        projection.references.variables.emplace(
            u"mu", std::vector<std::u16string>{u"M0"});

        const auto first = weftline::serialization::graph_projection_to_json(projection);
        const auto second = weftline::serialization::graph_projection_to_json(projection);
        EXPECT_EQ(first, second);

        const auto alpha = first.find("\"alpha\"");
        const auto mu = first.find("\"mu\"");
        const auto zeta = first.find("\"zeta\"");
        ASSERT_NE(alpha, std::string::npos);
        ASSERT_NE(mu, std::string::npos);
        ASSERT_NE(zeta, std::string::npos);
        EXPECT_LT(alpha, mu);
        EXPECT_LT(mu, zeta);
    }

    TEST(IrJson, WritesEmptyArrays)
    {
        // 契约：数组字段始终出现，空就是 []；可选标量（entryModuleId）缺失。
        weftline::graph::GraphProjection projection;

        weftline::graph::GraphChapter chapter;
        chapter.chapter_id = u"C";
        projection.chapters.push_back(chapter);

        weftline::graph::GraphModuleNode node;
        node.module_id = u"M";
        projection.modules.push_back(node);

        const auto json = weftline::serialization::graph_projection_to_json(projection);

        EXPECT_EQ(json.find("\"entryModuleId\""), std::string::npos);

        EXPECT_NE(json.find("\"entryCandidates\":[]"), std::string::npos);
        EXPECT_NE(json.find("\"scenes\":[]"), std::string::npos);
        EXPECT_NE(json.find("\"moduleIds\":[]"), std::string::npos);
        EXPECT_NE(json.find("\"chapters\":[{"), std::string::npos);
        EXPECT_NE(json.find("\"modules\":[{"), std::string::npos);
        EXPECT_NE(json.find("\"moduleEdges\":[]"), std::string::npos);
        EXPECT_NE(json.find("\"chapterEdges\":[]"), std::string::npos);
    }

} // namespace
