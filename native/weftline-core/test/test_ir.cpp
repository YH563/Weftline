#include <gtest/gtest.h>

#include <string>
#include <string_view>
#include <vector>

#include "weftline/ir/project_model_builder.hpp"
#include "weftline/projects/project_binder.hpp"
#include "weftline/projects/project_loader.hpp"
#include "weftline/projects/project_snapshot.hpp"

namespace {

    using weftline::projects::ProjectSnapshot;

    TEST(ProjectModelBuilder, BuildsFromWeftAndWStory)
    {
        ProjectSnapshot snapshot;
        snapshot.folder = "/tmp/雨夜";
        snapshot.version = 1;
        snapshot.documents["chapter.weft"] = R"(@title 第一章
@vars
    @var a false
@chars
    @char 萧影
@items
    @item 钥匙
@scenes
    @scene 医院
@module 开场
    [正文。]
@next -> END
)";
        snapshot.documents["project.wstory"] = R"(@project 雨夜
    @summary [概要。]
    @chapters
        @chapter 第一章 [章节描述。]
    @docs
        @doc 世界规则 [规则。]
    @chars
        @char 萧影 [主角。]
    @items
        @item 钥匙 [道具。]
    @scenes
        @scene 医院 [场景。]
    @vars
        @var a false [变量。]
)";

        const auto project = weftline::projects::load(snapshot);
        ASSERT_TRUE(project.diagnostics.empty());
        ASSERT_TRUE(project.wstory.has_value());

        const auto binding = weftline::projects::bind(
            project.weft_files, &*project.wstory, project.wstory_path);
        EXPECT_TRUE(binding.diagnostics.empty());

        const auto model =
            weftline::ir::build_project_model(project, binding.index);

        EXPECT_EQ(model.project.name, u"雨夜");
        ASSERT_TRUE(model.project.summary.has_value());
        EXPECT_EQ(*model.project.summary, u"概要。");
        ASSERT_EQ(model.project.docs.size(), 1u);
        EXPECT_EQ(model.project.docs[0].title, u"世界规则");
        EXPECT_EQ(model.project.docs[0].text, u"规则。");

        ASSERT_EQ(model.chapters.size(), 1u);
        EXPECT_EQ(model.chapters[0].id, u"第一章");
        EXPECT_EQ(model.chapters[0].title, u"第一章");
        ASSERT_TRUE(model.chapters[0].description.has_value());
        EXPECT_EQ(*model.chapters[0].description, u"章节描述。");
        ASSERT_EQ(model.chapters[0].module_ids.size(), 1u);
        EXPECT_EQ(model.chapters[0].module_ids[0], u"开场");

        ASSERT_EQ(model.symbols.variables.size(), 1u);
        EXPECT_EQ(model.symbols.variables[0].name, u"a");
        EXPECT_FALSE(model.symbols.variables[0].initial);
        ASSERT_TRUE(model.symbols.variables[0].description.has_value());
        EXPECT_EQ(*model.symbols.variables[0].description, u"变量。");

        ASSERT_EQ(model.symbols.characters.size(), 1u);
        ASSERT_TRUE(model.symbols.characters[0].description.has_value());
        EXPECT_EQ(*model.symbols.characters[0].description, u"主角。");

        ASSERT_EQ(model.symbols.items.size(), 1u);
        ASSERT_TRUE(model.symbols.items[0].description.has_value());
        EXPECT_EQ(*model.symbols.items[0].description, u"道具。");

        ASSERT_EQ(model.symbols.scenes.size(), 1u);
        ASSERT_TRUE(model.symbols.scenes[0].description.has_value());
        EXPECT_EQ(*model.symbols.scenes[0].description, u"场景。");
    }

    TEST(ProjectModelBuilder, FallsBackToFolderNameWithoutWStory)
    {
        ProjectSnapshot snapshot;
        snapshot.folder = "/tmp/MyProject/";
        snapshot.documents["chapter.weft"] = R"(@title 第一章
@module A
@next -> END
)";

        const auto project = weftline::projects::load(snapshot);
        ASSERT_TRUE(project.diagnostics.empty());
        EXPECT_FALSE(project.wstory.has_value());

        const auto binding = weftline::projects::bind(
            project.weft_files, nullptr, {});
        EXPECT_TRUE(binding.diagnostics.empty());

        const auto model =
            weftline::ir::build_project_model(project, binding.index);

        EXPECT_EQ(model.project.name, u"MyProject");
        EXPECT_FALSE(model.project.summary.has_value());
        EXPECT_TRUE(model.project.docs.empty());
        ASSERT_EQ(model.chapters.size(), 1u);
        EXPECT_FALSE(model.chapters[0].description.has_value());
    }

} // namespace
