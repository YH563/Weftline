using Weftline.Language;
using Weftline.Language.Diagnostics;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class ParserTests
{
    [Fact]
    public void ParsesGlobalsModulesAndContent()
    {
        const string source = """
        @globals
        @var trust_low false
        @var has_key false

        @title 相信
        @char 萧影
        @char 影子

        [
        雨水模糊了视线。
        ]

        萧影:[你来了。]

        @fig "assets/hospital.png"

        @set trust_low true

        @next(萧影):(trust_low)[相信他] -> 医院
            ()[沉默] -> 沉默
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        Assert.NotNull(result.Script.Globals);
        Assert.Equal(2, result.Script.Globals!.Variables.Count);

        var module = Assert.Single(result.Script.Modules);
        Assert.Equal("相信", module.Title);
        Assert.Equal(2, module.Characters.Count);

        var narration = Assert.Single(module.Content.OfType<NarrationSyntax>());
        Assert.Equal("雨水模糊了视线。", narration.Text);

        var dialogue = Assert.Single(module.Content.OfType<DialogueSyntax>());
        Assert.Equal("萧影", dialogue.Speaker);
        Assert.Equal("你来了。", dialogue.Text);

        var figure = Assert.Single(module.Content.OfType<FigureSyntax>());
        Assert.Equal("assets/hospital.png", figure.Path);

        var set = Assert.Single(module.Content.OfType<SetSyntax>());
        Assert.Equal("trust_low", set.Name);
        Assert.True(set.Value);

        Assert.NotNull(module.Exit);
        Assert.Equal(2, module.Exit!.RawLines.Count);
    }

    [Fact]
    public void ParsesInlineNarrationAndDialogue()
    {
        const string source = """
        @title 开场
        @char 萧影

        [简短旁白]
        萧影:[你好。]

        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Empty(result.Diagnostics);

        var module = Assert.Single(result.Script.Modules);
        Assert.Equal(
            "简短旁白",
            Assert.Single(module.Content.OfType<NarrationSyntax>()).Text);

        var dialogue = Assert.Single(module.Content.OfType<DialogueSyntax>());
        Assert.Equal("萧影", dialogue.Speaker);
        Assert.Equal("你好。", dialogue.Text);
    }

    [Fact]
    public void ReportsDuplicateModuleTitle()
    {
        const string source = """
        @title 相信
        @char 萧影
        [
        第一段。
        ]
        @next -> END

        @title 相信
        @char 萧影
        [
        第二段。
        ]
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.DuplicateModuleTitle &&
                 d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ReportsMissingExitBlock()
    {
        const string source = """
        @title 相信
        @char 萧影
        [
        没有出口。
        ]
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MissingExitBlock);
    }

    [Fact]
    public void ReportsUnterminatedTextBlock()
    {
        const string source = """
        @title 相信
        @char 萧影
        [
        没有闭合。
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UnterminatedTextBlock);
    }

    [Fact]
    public void ReportsVarOutsideGlobals()
    {
        const string source = """
        @title 相信
        @var trust_low false
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.VarOutsideGlobals);
    }

    [Fact]
    public void ReportsHeaderAfterContent()
    {
        const string source = """
        @title 相信
        @char 萧影
        [
        正文。
        ]
        @char 影子
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.HeaderAfterContent);
    }

    [Fact]
    public void ReportsUndeclaredCharacter()
    {
        const string source = """
        @title 相信
        [
        正文。
        ]
        萧影:[你好。]
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredCharacter);
    }

    [Fact]
    public void ReportsUndeclaredVariableInSet()
    {
        const string source = """
        @title 相信
        @char 萧影
        @set trust_low true
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredVariable);
    }
}
