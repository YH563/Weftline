#include <gtest/gtest.h>

#include <fstream>
#include <sstream>
#include <string>
#include <string_view>
#include <vector>

#include "weftline/ir/project_model_builder.hpp"
#include "weftline/ir/story_graph_builder.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/projects/project_snapshot.hpp"

namespace {

    using weftline::ir::BoolExprKind;
    using weftline::ir::ContentKind;
    using weftline::ir::ExitMode;
    using weftline::ir::IrModule;
    using weftline::ir::StoryGraph;
    using weftline::projects::ProjectSnapshot;

    [[nodiscard]] std::string read_file(const std::string& path)
    {
        std::ifstream file(path, std::ios::binary);
        if (!file) {
            return {};
        }
        std::ostringstream buffer;
        buffer << file.rdbuf();
        return buffer.str();
    }

    [[nodiscard]] const IrModule* find_module(
        const StoryGraph& graph, std::u16string_view id)
    {
        for (const auto& module : graph.modules) {
            if (module.id == id) {
                return &module;
            }
        }
        return nullptr;
    }

    TEST(StoryGraphBuilder, BuildsBelieveSampleGraph)
    {
        const std::string folder = WEFTLINE_SAMPLES_DIR;
        ProjectSnapshot snapshot;
        snapshot.folder = folder;
        snapshot.documents["believe.weft"] = read_file(folder + "/believe.weft");
        snapshot.documents["believe.wstory"] = read_file(folder + "/believe.wstory");

        const auto project = weftline::projects::load(snapshot);
        ASSERT_TRUE(project.diagnostics.empty());
        ASSERT_TRUE(project.wstory.has_value());

        const auto binding = weftline::projects::bind(
            project.weft_files, &*project.wstory, project.wstory_path);
        ASSERT_TRUE(binding.diagnostics.empty());

        const auto model =
            weftline::ir::build_project_model(project, binding.index);
        const auto graph = weftline::ir::build_story_graph(project, model);

        EXPECT_EQ(graph.schema_version, 2);
        EXPECT_EQ(graph.end, u"END");
        ASSERT_TRUE(graph.entry.has_value());
        EXPECT_EQ(*graph.entry, u"序");
        ASSERT_EQ(graph.globals.size(), 2u);
        EXPECT_EQ(graph.globals[0].name, u"trust_low");
        EXPECT_FALSE(graph.globals[0].initial);
        EXPECT_EQ(graph.globals[1].name, u"has_key");
        ASSERT_EQ(graph.modules.size(), 6u);

        const auto* prologue = find_module(graph, u"序");
        ASSERT_NE(prologue, nullptr);
        EXPECT_EQ(prologue->chapter_id, u"第一章 雨夜");
        ASSERT_TRUE(prologue->scene.has_value());
        EXPECT_EQ(*prologue->scene, u"街道");
        ASSERT_TRUE(prologue->exit.has_value());
        EXPECT_EQ(prologue->exit->mode, ExitMode::Auto);
        ASSERT_EQ(prologue->exit->branches.size(), 1u);
        EXPECT_EQ(prologue->exit->branches[0].target, u"相信");
        ASSERT_EQ(prologue->exit->branches[0].effects.size(), 1u);
        EXPECT_EQ(prologue->exit->branches[0].effects[0].name, u"trust_low");
        EXPECT_TRUE(prologue->exit->branches[0].effects[0].value);

        const auto* believe = find_module(graph, u"相信");
        ASSERT_NE(believe, nullptr);
        ASSERT_EQ(believe->content.size(), 4u);
        EXPECT_EQ(believe->content[0].kind, ContentKind::Narration);
        ASSERT_EQ(believe->content[0].segments.size(), 3u);
        EXPECT_EQ(believe->content[0].segments[0].text, u"雨水模糊了视线，");
        EXPECT_EQ(believe->content[0].segments[1].name, u"萧影");
        EXPECT_EQ(believe->content[1].kind, ContentKind::Dialogue);
        ASSERT_TRUE(believe->content[1].speaker.has_value());
        EXPECT_EQ(*believe->content[1].speaker, u"萧影");
        EXPECT_EQ(believe->content[3].kind, ContentKind::Figure);
        ASSERT_TRUE(believe->content[3].path.has_value());
        EXPECT_EQ(*believe->content[3].path, u"assets/hospital.png");

        ASSERT_TRUE(believe->exit.has_value());
        EXPECT_EQ(believe->exit->mode, ExitMode::Choice);
        ASSERT_TRUE(believe->exit->character.has_value());
        EXPECT_EQ(*believe->exit->character, u"萧影");
        ASSERT_EQ(believe->exit->branches.size(), 2u);

        const auto& choice = believe->exit->branches[0];
        ASSERT_TRUE(choice.condition.has_value());
        EXPECT_EQ(choice.condition->kind, BoolExprKind::Ref);
        EXPECT_EQ(choice.condition->name, u"trust_low");
        ASSERT_TRUE(choice.label.has_value());
        EXPECT_EQ(*choice.label, u"相信他");
        EXPECT_EQ(choice.target, u"医院");
        ASSERT_EQ(choice.effects.size(), 1u);
        EXPECT_EQ(choice.effects[0].name, u"has_key");
        EXPECT_TRUE(choice.effects[0].value);

        const auto* hospital = find_module(graph, u"医院");
        ASSERT_NE(hospital, nullptr);
        ASSERT_TRUE(hospital->exit.has_value());
        EXPECT_EQ(hospital->exit->mode, ExitMode::Auto);
        ASSERT_EQ(hospital->exit->branches.size(), 2u);
        ASSERT_TRUE(hospital->exit->branches[0].condition.has_value());
        EXPECT_EQ(hospital->exit->branches[0].condition->kind, BoolExprKind::Ref);
        EXPECT_EQ(hospital->exit->branches[0].condition->name, u"has_key");
        EXPECT_EQ(hospital->exit->branches[0].target, u"地下层");
        EXPECT_FALSE(hospital->exit->branches[1].condition.has_value());
        EXPECT_EQ(hospital->exit->branches[1].target, u"走廊");

        const auto custom_entry = weftline::ir::build_story_graph(
            project, model, std::u16string(u"医院"));
        ASSERT_TRUE(custom_entry.entry.has_value());
        EXPECT_EQ(*custom_entry.entry, u"医院");
    }

    TEST(StoryGraphBuilder, ConvertsBooleanExpressions)
    {
        ProjectSnapshot snapshot;
        snapshot.folder = "test";
        snapshot.documents["chapter.weft"] = R"(@title T
@vars
    @var a false
    @var b false
@module M
    [x]
@next():(not a and b) -> END
    () -> END
)";

        const auto project = weftline::projects::load(snapshot);
        ASSERT_TRUE(project.diagnostics.empty());
        const auto binding = weftline::projects::bind(
            project.weft_files, nullptr, {});
        ASSERT_TRUE(binding.diagnostics.empty());
        const auto model =
            weftline::ir::build_project_model(project, binding.index);
        const auto graph = weftline::ir::build_story_graph(project, model);

        ASSERT_EQ(graph.modules.size(), 1u);
        const auto* module = find_module(graph, u"M");
        ASSERT_NE(module, nullptr);
        ASSERT_TRUE(module->exit.has_value());
        ASSERT_EQ(module->exit->branches.size(), 2u);

        const auto& condition = module->exit->branches[0].condition;
        ASSERT_TRUE(condition.has_value());
        EXPECT_EQ(condition->kind, BoolExprKind::And);
        ASSERT_NE(condition->left, nullptr);
        ASSERT_NE(condition->right, nullptr);
        EXPECT_EQ(condition->left->kind, BoolExprKind::Not);
        ASSERT_NE(condition->left->operand, nullptr);
        EXPECT_EQ(condition->left->operand->kind, BoolExprKind::Ref);
        EXPECT_EQ(condition->left->operand->name, u"a");
        EXPECT_EQ(condition->right->kind, BoolExprKind::Ref);
        EXPECT_EQ(condition->right->name, u"b");
    }

} // namespace
