using Weftline.Language.Diagnostics;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class WStoryParserTests
{
    [Fact]
    public void ParsesProjectSummaryDocsAndGroups()
    {
        const string source = """
        @project 雨夜
            @summary [一个关于信任的故事。]

            @docs
                @doc 世界规则 [医院在雨夜不关门。]

            @chars
                @char 萧影 [主角。]
                @char 影子 [萧影的旧识。]

            @items
                @item 医院钥匙 [医院地下室的钥匙。]

            @scenes
                @scene 医院 [雨夜里的医院。]

            @vars
                @var 信任 false [萧影是否信任影子。]
                @var 有钥匙 false [是否拿到了医院钥匙。]
        """;

        var result = WeftlineCompiler.Parse(source, "project.wstory");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WStoryFileSyntax>(result.Script);

        Assert.Equal("雨夜", file.ProjectName);
        Assert.Equal("一个关于信任的故事。", file.Summary?.Text);
        var doc = Assert.Single(file.Docs);
        Assert.Equal("世界规则", doc.Title);
        Assert.Equal("医院在雨夜不关门。", doc.Body.Text);

        Assert.Equal(2, file.Characters.Count);
        Assert.Equal("主角。", file.Characters[0].Description?.Text);
        Assert.Single(file.Items);
        Assert.Single(file.Scenes);
        Assert.Equal(2, file.Variables.Count);
        Assert.False(file.Variables[0].Value);
        Assert.Equal("萧影是否信任影子。", file.Variables[0].Description?.Text);
    }

    [Fact]
    public void ParsesMultilineDescriptionWithAttachedBrackets()
    {
        const string source = """
        @project 雨夜

        @chars
            @char 萧影 [主角。
            也是叙述者。]
        """;

        var result = WeftlineCompiler.Parse(source, "project.wstory");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WStoryFileSyntax>(result.Script);
        var character = Assert.Single(file.Characters);
        Assert.Equal("主角。\n也是叙述者。", character.Description?.Text);
    }

    [Fact]
    public void ReportsMissingProject()
    {
        const string source = """
        @chars
            @char 萧影
        """;

        var result = WeftlineCompiler.Parse(source, "project.wstory");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MalformedDirective);
    }

    [Fact]
    public void ReportsVariableWithoutValue()
    {
        const string source = """
        @project 雨夜

        @vars
            @var 信任 [没有初始值。]
        """;

        var result = WeftlineCompiler.Parse(source, "project.wstory");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MalformedDirective);
    }

    [Fact]
    public void ReportsSetInWStory()
    {
        const string source = """
        @project 雨夜

        @vars
            @var 信任 false

        @set 信任 true
        """;

        var result = WeftlineCompiler.Parse(source, "project.wstory");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.SetInProjectFile);
    }
}
