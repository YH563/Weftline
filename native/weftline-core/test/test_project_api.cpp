#include <gtest/gtest.h>

#include <string>

#include "weftline/core.h"

TEST(ProjectApi, ReturnsDiagnosticsJson)
{
    WeftProject* project = weft_project_create();
    ASSERT_NE(project, nullptr);

    weft_project_set_folder(project, "test");
    weft_project_set_document(
        project,
        "chapter.weft",
        "@title 第一章\n"
        "@module A\n"
        "    [{钥匙}。]\n"
        "@next -> END\n");

    char* response = weft_project_compile(project, 1, 1);
    ASSERT_NE(response, nullptr);

    const std::string json(response);
    weft_free(response);
    weft_project_destroy(project);

    EXPECT_NE(json.find("\"diagnostics\""), std::string::npos);
    EXPECT_NE(json.find("\"hasErrors\":true"), std::string::npos);
    EXPECT_NE(json.find("WFT2014"), std::string::npos);
    EXPECT_NE(json.find("\"filePath\":\"chapter.weft\""), std::string::npos);
}

TEST(ProjectApi, ReturnsSymbolsWithDescriptionsAndLocations)
{
    WeftProject* project = weft_project_create();
    ASSERT_NE(project, nullptr);

    weft_project_set_document(
        project,
        "story.wstory",
        "@project 测试\n"
        "    @chapters\n"
        "        @chapter 第一章 [开篇。]\n"
        "    @chars\n"
        "        @char 萧影 [主角。]\n"
        "    @vars\n"
        "        @var 信任 false [是否信任。]\n");

    weft_project_set_document(
        project,
        "chapter.weft",
        "@title 第一章\n"
        "@vars\n"
        "    @var 信任 false\n"
        "@chars\n"
        "    @char 萧影\n"
        "@module A\n"
        "    [{萧影}。]\n"
        "@next -> END\n");

    char* response = weft_project_compile(project, 0, 1);
    ASSERT_NE(response, nullptr);

    const std::string json(response);
    weft_free(response);
    weft_project_destroy(project);

    EXPECT_NE(json.find("\"symbols\":{"), std::string::npos);
    EXPECT_NE(
        json.find("\"variables\":[{\"name\":\"信任\",\"initial\":false"),
        std::string::npos);
    EXPECT_NE(json.find("\"description\":\"是否信任。\""), std::string::npos);
    EXPECT_NE(
        json.find("\"characters\":[{\"name\":\"萧影\""),
        std::string::npos);
    EXPECT_NE(json.find("\"description\":\"主角。\""), std::string::npos);
    EXPECT_NE(
        json.find("\"chapters\":[{\"id\":\"第一章\",\"title\":\"第一章\""),
        std::string::npos);
    EXPECT_NE(json.find("\"description\":\"开篇。\""), std::string::npos);
    EXPECT_NE(json.find("\"filePath\":\"chapter.weft\""), std::string::npos);
}

TEST(ProjectApi, ValidProjectHasNoErrors)
{
    WeftProject* project = weft_project_create();
    ASSERT_NE(project, nullptr);

    weft_project_set_document(
        project,
        "chapter.weft",
        "@title 第一章\n"
        "@module A\n"
        "@next -> END\n");

    char* response = weft_project_compile(project, 0, 1);
    ASSERT_NE(response, nullptr);

    const std::string json(response);
    weft_free(response);
    weft_project_destroy(project);

    EXPECT_NE(json.find("\"hasErrors\":false"), std::string::npos);
    EXPECT_NE(json.find("\"diagnostics\":[]"), std::string::npos);
    EXPECT_NE(json.find("\"graph\":{"), std::string::npos);
    EXPECT_NE(json.find("\"projection\":{"), std::string::npos);
}

TEST(ProjectApi, StrictErrorsOmitGraph)
{
    WeftProject* project = weft_project_create();
    ASSERT_NE(project, nullptr);

    weft_project_set_document(
        project,
        "chapter.weft",
        "@title 第一章\n"
        "@module A\n"
        "@next -> Missing\n");

    char* response = weft_project_compile(project, 0, 1);
    ASSERT_NE(response, nullptr);

    const std::string json(response);
    weft_free(response);
    weft_project_destroy(project);

    EXPECT_NE(json.find("\"hasErrors\":true"), std::string::npos);
    EXPECT_EQ(json.find("\"graph\""), std::string::npos);
    EXPECT_EQ(json.find("\"projection\""), std::string::npos);
}

TEST(ProjectApi, UpdatesDiagnosticsAfterDocumentChange)
{
    WeftProject* project = weft_project_create();
    ASSERT_NE(project, nullptr);

    const char* valid =
        "@title 第一章\n"
        "@module A\n"
        "@next -> END\n";
    const char* invalid =
        "@title 第一章\n"
        "@module A\n"
        "@next -> Missing\n";

    weft_project_set_document(project, "chapter.weft", valid);
    char* first = weft_project_compile(project, 0, 0);
    ASSERT_NE(first, nullptr);
    const std::string first_json(first);
    weft_free(first);
    EXPECT_NE(first_json.find("\"hasErrors\":false"), std::string::npos);

    weft_project_set_document(project, "chapter.weft", invalid);
    char* second = weft_project_compile(project, 0, 0);
    ASSERT_NE(second, nullptr);
    const std::string second_json(second);
    weft_free(second);
    EXPECT_NE(second_json.find("\"hasErrors\":true"), std::string::npos);
    EXPECT_NE(second_json.find("WFT4001"), std::string::npos);

    weft_project_set_document(project, "chapter.weft", valid);
    char* third = weft_project_compile(project, 0, 0);
    ASSERT_NE(third, nullptr);
    const std::string third_json(third);
    weft_free(third);
    EXPECT_NE(third_json.find("\"hasErrors\":false"), std::string::npos);

    weft_project_destroy(project);
}
