#include <gtest/gtest.h>

#include <algorithm>
#include <memory>
#include <string>
#include <string_view>
#include <variant>
#include <vector>

#include "weftline/ir/graph_projection_builder.hpp"
#include "weftline/ir/project_model_builder.hpp"
#include "weftline/ir/story_graph_builder.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    using weftline::ir::GraphChapter;
    using weftline::ir::GraphChapterFlags;
    using weftline::ir::GraphModuleFlags;
    using weftline::ir::GraphModuleNode;
    using weftline::ir::GraphProjection;
    using weftline::ir::has_flag;
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

    struct ProjectionResult
    {
        GraphProjection projection;
    };

    [[nodiscard]] GraphProjection build_projection(std::vector<ProjectFile> files)
    {
        LoadedProject project;
        project.folder = "test";
        project.weft_files = files;

        const auto binding =
            weftline::projects::bind(project.weft_files, nullptr, {});
        EXPECT_TRUE(binding.diagnostics.empty());

        const auto model =
            weftline::ir::build_project_model(project, binding.index);
        const auto graph = weftline::ir::build_story_graph(project, model);
        return weftline::ir::build_graph_projection(model, graph);
    }

    [[nodiscard]] const GraphChapter* find_chapter(
        const GraphProjection& projection, std::u16string_view id)
    {
        for (const auto& chapter : projection.chapters) {
            if (chapter.chapter_id == id) {
                return &chapter;
            }
        }
        return nullptr;
    }

    [[nodiscard]] const GraphModuleNode* find_module(
        const GraphProjection& projection, std::u16string_view id)
    {
        for (const auto& module : projection.modules) {
            if (module.module_id == id) {
                return &module;
            }
        }
        return nullptr;
    }

    TEST(GraphProjection, BuildsChaptersModulesEdgesAndAggregates)
    {
        auto first = parse_file("chapter1.weft", R"(@title 第一章
@vars
    @var x false

@chars
    @char 小明

@scenes
    @scene 房间

@module A
    @scene 房间
    [小明在房间。]
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
        const auto projection = build_projection(std::move(files));

        ASSERT_TRUE(projection.entry_module_id.has_value());
        EXPECT_EQ(*projection.entry_module_id, u"A");

        const auto* first_chapter = find_chapter(projection, u"第一章");
        ASSERT_NE(first_chapter, nullptr);
        EXPECT_EQ(
            first_chapter->module_ids,
            (std::vector<std::u16string>{u"A", u"B"}));
        EXPECT_TRUE(has_flag(first_chapter->flags, GraphChapterFlags::Entry));
        EXPECT_TRUE(has_flag(first_chapter->flags, GraphChapterFlags::HasOutgoing));
        EXPECT_FALSE(has_flag(first_chapter->flags, GraphChapterFlags::HasIncoming));
        EXPECT_FALSE(has_flag(first_chapter->flags, GraphChapterFlags::HasEndEdge));

        const auto* second_chapter = find_chapter(projection, u"第二章");
        ASSERT_NE(second_chapter, nullptr);
        EXPECT_EQ(
            second_chapter->module_ids,
            (std::vector<std::u16string>{u"C", u"D"}));
        EXPECT_TRUE(has_flag(second_chapter->flags, GraphChapterFlags::HasIncoming));
        EXPECT_FALSE(has_flag(second_chapter->flags, GraphChapterFlags::HasOutgoing));
        EXPECT_TRUE(has_flag(second_chapter->flags, GraphChapterFlags::HasEndEdge));

        const auto* module_a = find_module(projection, u"A");
        ASSERT_NE(module_a, nullptr);
        EXPECT_TRUE(has_flag(module_a->flags, GraphModuleFlags::Entry));
        EXPECT_FALSE(has_flag(module_a->flags, GraphModuleFlags::TargetsEnd));
        EXPECT_EQ(module_a->branch_count, 2);
        ASSERT_TRUE(module_a->summary.first_line.has_value());
        EXPECT_EQ(*module_a->summary.first_line, u"小明在房间。");
        EXPECT_EQ(module_a->summary.dialogue_count, 1);
        EXPECT_EQ(module_a->summary.choice_count, 0);

        const auto* module_c = find_module(projection, u"C");
        ASSERT_NE(module_c, nullptr);
        EXPECT_TRUE(has_flag(module_c->flags, GraphModuleFlags::TargetsEnd));

        const auto edges_from_a = static_cast<std::size_t>(std::count_if(
            projection.module_edges.begin(), projection.module_edges.end(),
            [](const auto& edge) { return edge.from_module == u"A"; }));
        EXPECT_EQ(edges_from_a, 2u);
        for (const auto& edge : projection.module_edges) {
            if (edge.from_module == u"A") {
                EXPECT_FALSE(edge.external);
            }
        }

        const auto edges_from_b = static_cast<std::size_t>(std::count_if(
            projection.module_edges.begin(), projection.module_edges.end(),
            [](const auto& edge) { return edge.from_module == u"B"; }));
        EXPECT_EQ(edges_from_b, 3u);
        for (const auto& edge : projection.module_edges) {
            if (edge.from_module == u"B") {
                EXPECT_TRUE(edge.external);
            }
        }

        EXPECT_EQ(projection.chapter_edges.size(), 2u);

        const auto character = projection.references.characters.find(u"小明");
        ASSERT_NE(character, projection.references.characters.end());
        EXPECT_EQ(character->second, (std::vector<std::u16string>{u"A"}));

        const auto scene = projection.references.scenes.find(u"房间");
        ASSERT_NE(scene, projection.references.scenes.end());
        EXPECT_EQ(scene->second, (std::vector<std::u16string>{u"A"}));

        const auto variable = projection.references.variables.find(u"x");
        ASSERT_NE(variable, projection.references.variables.end());
        EXPECT_EQ(variable->second, (std::vector<std::u16string>{u"A", u"B"}));
    }

    TEST(GraphProjection, MarksUnreachableAndCannotReachEndModules)
    {
        auto file = parse_file("chapter.weft", R"(@title 第一章
@module A
@next -> END

@module B
@next -> B
)");

        std::vector<ProjectFile> files;
        files.push_back(std::move(file));
        const auto projection = build_projection(std::move(files));

        const auto* module_a = find_module(projection, u"A");
        ASSERT_NE(module_a, nullptr);
        EXPECT_TRUE(has_flag(module_a->flags, GraphModuleFlags::Entry));
        EXPECT_TRUE(has_flag(module_a->flags, GraphModuleFlags::TargetsEnd));
        EXPECT_FALSE(has_flag(module_a->flags, GraphModuleFlags::Unreachable));
        EXPECT_FALSE(has_flag(module_a->flags, GraphModuleFlags::CannotReachEnd));

        const auto* module_b = find_module(projection, u"B");
        ASSERT_NE(module_b, nullptr);
        EXPECT_TRUE(has_flag(module_b->flags, GraphModuleFlags::Unreachable));
        EXPECT_TRUE(has_flag(module_b->flags, GraphModuleFlags::CannotReachEnd));
    }

} // namespace
