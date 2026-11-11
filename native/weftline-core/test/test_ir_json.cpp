#include <gtest/gtest.h>

#include <memory>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include "weftline/ir/graph_projection_builder.hpp"
#include "weftline/ir/ir_json.hpp"
#include "weftline/ir/project_model_builder.hpp"
#include "weftline/ir/story_graph_builder.hpp"
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
        const auto json = weftline::ir::story_graph_to_json(graph);

        EXPECT_NE(json.find("\"schemaVersion\":2"), std::string::npos);
        EXPECT_NE(json.find("\"end\":\"END\""), std::string::npos);
        EXPECT_NE(json.find("\"entry\":\"M\""), std::string::npos);
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
            weftline::ir::build_graph_projection(model, graph);
        const auto json = weftline::ir::graph_projection_to_json(projection);

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

} // namespace
