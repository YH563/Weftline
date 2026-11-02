using Weftline.Language.Diagnostics;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class ParserTests
{
    [Fact]
    public void ParsesWeftDeclarationsAndModules()
    {
        const string source = """
        @vars
            @var trust_low false
            @var has_key false

        @chars
            @char 萧影
            @char 影子

        @items
            @item 医院钥匙

        @scenes
            @scene 医院

        @title 相信
            @scene 医院
            [雨水模糊了视线，{萧影}看见了{医院钥匙}。]
            萧影:[你来了。]
            @fig "assets/hospital.png"
        @next(萧影):(trust_low)[相信他] -> 医院 { @set trust_low false }
            ()[沉默] -> 沉默
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);

        Assert.Equal(2, file.Variables.Count);
        Assert.Equal(2, file.Characters.Count);
        Assert.Single(file.Items);
        Assert.Single(file.Scenes);

        var module = Assert.Single(file.Modules);
        Assert.Equal("相信", module.Title);
        Assert.Equal("医院", module.Scene);

        var narration = Assert.Single(module.Content.OfType<NarrationSyntax>());
        Assert.Equal("雨水模糊了视线，萧影看见了医院钥匙。", narration.Text);
        Assert.Contains(narration.Segments, segment => segment is TextReferenceSyntax { Name: "萧影" });
        Assert.Contains(narration.Segments, segment => segment is TextReferenceSyntax { Name: "医院钥匙" });

        var dialogue = Assert.Single(module.Content.OfType<DialogueSyntax>());
        Assert.Equal("萧影", dialogue.Speaker);
        Assert.Equal("你来了。", dialogue.Text);

        var figure = Assert.Single(module.Content.OfType<FigureSyntax>());
        Assert.Equal("assets/hospital.png", figure.Path);

        var exit = Assert.IsType<ExitSyntax>(module.Exit);
        Assert.Equal("萧影", exit.Character);
        Assert.Equal(2, exit.Branches.Count);

        var first = exit.Branches[0];
        Assert.Equal("相信他", first.Label);
        Assert.Equal("医院", first.Target);
        Assert.Equal("trust_low", Assert.IsType<BoolRefSyntax>(first.Condition).Name);
        var effect = Assert.Single(first.Effects);
        Assert.Equal("trust_low", effect.Name);
        Assert.False(effect.Value);
    }

    [Fact]
    public void ParsesInlineNarrationAndDialogue()
    {
        const string source = """
        @chars
            @char 萧影

        @title 开场
            [简短旁白]
            萧影:[你好。]
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);
        var module = Assert.Single(file.Modules);

        Assert.Equal(
            "简短旁白",
            Assert.Single(module.Content.OfType<NarrationSyntax>()).Text);

        var dialogue = Assert.Single(module.Content.OfType<DialogueSyntax>());
        Assert.Equal("萧影", dialogue.Speaker);
        Assert.Equal("你好。", dialogue.Text);
    }

    [Fact]
    public void ParsesMultilineTextWithAttachedBrackets()
    {
        const string source = """
        @vars
            @var a false

        @title 测试
            [第一行
            第二行]
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);
        var narration = Assert.Single(Assert.Single(file.Modules).Content.OfType<NarrationSyntax>());
        Assert.Equal("第一行\n第二行", narration.Text);
    }

    [Fact]
    public void ParsesReferenceEscapes()
    {
        const string source = """
        @chars
            @char 萧影

        @title 测试
            [{萧影}说：{{别怕}}。]
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);
        var narration = Assert.Single(Assert.Single(file.Modules).Content.OfType<NarrationSyntax>());
        Assert.Equal("萧影说：{别怕}。", narration.Text);
        Assert.Single(narration.Segments.OfType<TextReferenceSyntax>());
    }

    [Fact]
    public void ReportsVariableWithoutInitialValue()
    {
        const string source = """
        @vars
            @var 信任
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MalformedDirective);
    }

    [Fact]
    public void ReportsDuplicateModuleTitle()
    {
        const string source = """
        @chars
            @char 萧影

        @title 相信
            [第一段。]
        @next -> END

        @title 相信
            [第二段。]
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.DuplicateModuleTitle);
    }

    [Fact]
    public void ReportsMissingExitBlock()
    {
        const string source = """
        @chars
            @char 萧影

        @title 相信
            [没有出口。]
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MissingExitBlock);
    }

    [Fact]
    public void ReportsUnterminatedTextBlock()
    {
        const string source = """
        @chars
            @char 萧影

        @title 相信
            [没有闭合。
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UnterminatedTextBlock);
    }

    [Fact]
    public void ReportsUnterminatedReference()
    {
        const string source = """
        @chars
            @char 萧影

        @title 相信
            [他{萧影 走了。]
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UnterminatedReference);
    }

    [Fact]
    public void ReportsDeclarationsAfterModules()
    {
        const string source = """
        @title 相信
            [正文。]
        @vars
            @var a false
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.VarOutsideGlobals);
    }

    [Fact]
    public void ReportsHeaderAfterContent()
    {
        const string source = """
        @chars
            @char 萧影

        @title 相信
            [正文。]
            @scene 医院
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.HeaderAfterContent);
    }

    [Fact]
    public void ReportsInvalidTitleCharacter()
    {
        const string source = """
        @title 相信{他}
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidTitleCharacter);
    }

    [Fact]
    public void ReportsEndAsTitle()
    {
        const string source = """
        @title END
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidEnd);
    }

    [Fact]
    public void ReportsSetInContent()
    {
        const string source = """
        @vars
            @var a false

        @title 测试
            @set a true
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.SetInContent);
    }
}
