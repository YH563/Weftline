#include <gtest/gtest.h>

#include <fstream>
#include <sstream>
#include <string>
#include <string_view>

#include "weftline/ir/ir_json.hpp"
#include "weftline/ir/project_model_builder.hpp"
#include "weftline/ir/runtime_projection_builder.hpp"
#include "weftline/ir/story_graph_builder.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/projects/project_snapshot.hpp"

namespace {

    using weftline::ir::BoolExprKind;
    using weftline::ir::ContentKind;
    using weftline::ir::ExitMode;
    using weftline::ir::RuntimeModule;
    using weftline::ir::RuntimeProjection;
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

    [[nodiscard]] const RuntimeModule* find_module(
        const RuntimeProjection& runtime, std::u16string_view id)
    {
        for (const auto& module : runtime.modules) {
            if (module.id == id) {
                return &module;
            }
        }
        return nullptr;
    }

    [[nodiscard]] bool build_sample_runtime(RuntimeProjection& runtime)
    {
        const std::string folder = WEFTLINE_SAMPLES_DIR;

        ProjectSnapshot snapshot;
        snapshot.folder = folder;
        snapshot.documents["believe.weft"] = read_file(folder + "/believe.weft");
        snapshot.documents["believe.wstory"] = read_file(folder + "/believe.wstory");

        const auto project = weftline::projects::load(snapshot);
        if (!project.diagnostics.empty() || !project.wstory) {
            return false;
        }

        const auto binding = weftline::projects::bind(
            project.weft_files, &*project.wstory, project.wstory_path);
        if (!binding.diagnostics.empty()) {
            return false;
        }

        const auto model =
            weftline::ir::build_project_model(project, binding.index);
        const auto graph = weftline::ir::build_story_graph(project, model);
        runtime = weftline::ir::build_runtime_projection(model, graph);
        return true;
    }

    TEST(RuntimeProjection, BuildsFromBelieveSample)
    {
        RuntimeProjection runtime;
        ASSERT_TRUE(build_sample_runtime(runtime));

        EXPECT_EQ(runtime.schema_version, 1);
        EXPECT_EQ(runtime.project_name, u"雨夜");
        EXPECT_EQ(runtime.end, u"END");
        ASSERT_TRUE(runtime.entry.has_value());
        EXPECT_EQ(*runtime.entry, u"序");

        ASSERT_EQ(runtime.chapters.size(), 1u);
        EXPECT_EQ(runtime.chapters[0].id, u"第一章 雨夜");
        EXPECT_EQ(runtime.chapters[0].title, u"第一章 雨夜");

        ASSERT_EQ(runtime.globals.size(), 2u);
        EXPECT_EQ(runtime.globals[0].name, u"trust_low");
        EXPECT_FALSE(runtime.globals[0].initial);
        EXPECT_EQ(runtime.globals[1].name, u"has_key");
        EXPECT_FALSE(runtime.globals[1].initial);

        ASSERT_EQ(runtime.modules.size(), 6u);

        const auto* prologue = find_module(runtime, u"序");
        ASSERT_NE(prologue, nullptr);
        ASSERT_EQ(prologue->content.size(), 1u);
        EXPECT_EQ(prologue->content[0].kind, ContentKind::Narration);
        ASSERT_TRUE(prologue->exit.has_value());
        EXPECT_EQ(prologue->exit->mode, ExitMode::Auto);
        ASSERT_EQ(prologue->exit->branches.size(), 1u);
        EXPECT_EQ(prologue->exit->branches[0].target, u"相信");
        ASSERT_EQ(prologue->exit->branches[0].effects.size(), 1u);
        EXPECT_EQ(prologue->exit->branches[0].effects[0].name, u"trust_low");
        EXPECT_TRUE(prologue->exit->branches[0].effects[0].value);

        const auto* believe = find_module(runtime, u"相信");
        ASSERT_NE(believe, nullptr);
        ASSERT_EQ(believe->content.size(), 4u);
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

        const auto& fallback = believe->exit->branches[1];
        EXPECT_FALSE(fallback.condition.has_value());
        ASSERT_TRUE(fallback.label.has_value());
        EXPECT_EQ(*fallback.label, u"沉默");
        EXPECT_EQ(fallback.target, u"沉默");
        EXPECT_TRUE(fallback.effects.empty());
    }

    TEST(RuntimeProjection, JsonOmitsSourceLocations)
    {
        RuntimeProjection runtime;
        ASSERT_TRUE(build_sample_runtime(runtime));

        const auto json = weftline::ir::runtime_projection_to_json(runtime);

        EXPECT_NE(json.find("\"schemaVersion\":1"), std::string::npos);
        EXPECT_NE(json.find("\"projectName\":\"雨夜\""), std::string::npos);
        EXPECT_NE(json.find("\"entry\":\"序\""), std::string::npos);
        EXPECT_NE(json.find("\"end\":\"END\""), std::string::npos);
        EXPECT_NE(
            json.find("\"chapters\":[{\"id\":\"第一章 雨夜\",\"title\":\"第一章 雨夜\"}]"),
            std::string::npos);
        EXPECT_NE(json.find("\"path\":\"assets/hospital.png\""), std::string::npos);
        EXPECT_NE(
            json.find("\"condition\":{\"kind\":\"ref\",\"name\":\"trust_low\"}"),
            std::string::npos);
        EXPECT_NE(
            json.find("\"effects\":[{\"name\":\"has_key\",\"value\":true}]"),
            std::string::npos);

        EXPECT_EQ(json.find("\"source\""), std::string::npos);
        EXPECT_EQ(json.find("\"filePath\""), std::string::npos);
        EXPECT_EQ(json.find("\"start\""), std::string::npos);
        EXPECT_EQ(json.find("\"length\""), std::string::npos);
    }

} // namespace
