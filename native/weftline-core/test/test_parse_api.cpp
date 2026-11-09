#include <gtest/gtest.h>

#include <string>

#include "weftline/core.h"

TEST(ParseApi, ReturnsMetadataForValidFile)
{
    char* response = weft_parse_json(
        "chapter.weft",
        "@title 第一章\n"
        "@module A\n"
        "    [文本]\n"
        "@next -> END\n",
        7);
    ASSERT_NE(response, nullptr);

    const std::string json(response);
    weft_free(response);

    EXPECT_NE(json.find("\"schemaVersion\":1"), std::string::npos);
    EXPECT_NE(json.find("\"filePath\":\"chapter.weft\""), std::string::npos);
    EXPECT_NE(json.find("\"requestVersion\":7"), std::string::npos);
    EXPECT_NE(json.find("\"hasErrors\":false"), std::string::npos);
    EXPECT_NE(json.find("\"diagnostics\":[]"), std::string::npos);
}

TEST(ParseApi, ReturnsSyntaxDiagnostics)
{
    char* response = weft_parse_json(
        "chapter.weft",
        "@title 第一章\n"
        "@module A\n"
        "    [未闭合\n",
        11);
    ASSERT_NE(response, nullptr);

    const std::string json(response);
    weft_free(response);

    EXPECT_NE(json.find("\"requestVersion\":11"), std::string::npos);
    EXPECT_NE(json.find("\"hasErrors\":true"), std::string::npos);
    EXPECT_NE(json.find("\"code\":\"WFT1002\""), std::string::npos);
    EXPECT_NE(json.find("\"severity\":\"error\""), std::string::npos);
    EXPECT_NE(json.find("\"filePath\":\"chapter.weft\""), std::string::npos);
}

TEST(ParseApi, TreatsNullTextAsEmpty)
{
    char* response = weft_parse_json("chapter.weft", nullptr, 3);
    ASSERT_NE(response, nullptr);

    const std::string json(response);
    weft_free(response);

    EXPECT_NE(json.find("\"requestVersion\":3"), std::string::npos);
    EXPECT_NE(json.find("\"hasErrors\":true"), std::string::npos);
}
