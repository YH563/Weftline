using Weftline.Language;
using Weftline.Language.Diagnostics;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class ExitParserTests
{
    [Fact]
    public void ParsesAutoExitWithConditionAndFallback()
    {
        const string source = """
        @title 第15章
        @vars
            @var has_key false

        @module 医院
        @next():(has_key) -> 地下层
            () -> 走廊

        @module 地下层
        @next -> END

        @module 走廊
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);

        var hospital = Assert.Single(file.Modules, m => m.Title == "医院");
        var exit = Assert.IsType<ExitSyntax>(hospital.Exit);

        Assert.Null(exit.Character);
        Assert.False(exit.IsChoiceMode);
        Assert.Equal(2, exit.Branches.Count);

        var first = exit.Branches[0];
        Assert.Equal("has_key", Assert.IsType<BoolRefSyntax>(first.Condition).Name);
        Assert.Equal("地下层", first.Target);
        Assert.Null(first.Label);

        var second = exit.Branches[1];
        Assert.Null(second.Condition);
        Assert.Equal("走廊", second.Target);
        Assert.Null(second.Label);
    }

    [Fact]
    public void ParsesShorthandExit()
    {
        const string source = """
        @title 第16章
        @module 开场
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);
        var exit = Assert.IsType<ExitSyntax>(Assert.Single(file.Modules).Exit);

        Assert.Null(exit.Character);
        Assert.False(exit.IsChoiceMode);
        Assert.Single(exit.Branches);

        var branch = exit.Branches[0];
        Assert.Null(branch.Condition);
        Assert.Null(branch.Label);
        Assert.Equal("END", branch.Target);
        Assert.Empty(branch.Effects);
    }

    [Fact]
    public void ParsesShorthandExitWithEffects()
    {
        const string source = """
        @title 第17章
        @vars
            @var a false

        @module 开场
        @next -> END { @set a true }
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);
        var branch = Assert.IsType<ExitSyntax>(Assert.Single(file.Modules).Exit).Branches[0];
        var effect = Assert.Single(branch.Effects);
        Assert.Equal("a", effect.Name);
        Assert.True(effect.Value);
    }

    [Fact]
    public void ParsesConditionPrecedence()
    {
        const string source = """
        @title 第18章
        @vars
            @var a false
            @var b false
            @var c false

        @module 测试
        @next():(a and not b or c) -> 结束
            () -> END

        @module 结束
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);

        var module = Assert.Single(file.Modules, m => m.Title == "测试");
        var branch = Assert.IsType<ExitSyntax>(module.Exit).Branches[0];
        var or = Assert.IsType<OrExprSyntax>(branch.Condition);

        var and = Assert.IsType<AndExprSyntax>(or.Left);
        Assert.Equal("a", Assert.IsType<BoolRefSyntax>(and.Left).Name);

        var not = Assert.IsType<NotExprSyntax>(and.Right);
        Assert.Equal("b", Assert.IsType<BoolRefSyntax>(not.Operand).Name);
        Assert.Equal("c", Assert.IsType<BoolRefSyntax>(or.Right).Name);
    }

    [Fact]
    public void ParsesInlineAndMultilineEffects()
    {
        const string source = """
        @title 第19章
        @vars
            @var a false
            @var b false

        @module 开场
        @next(萧影):[继续] -> 结局 { @set a true }
            ()[结束] -> END

        @module 结局
        @next(萧影):[继续] -> END {
            @set a true
            @set b true
        }
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);

        var opening = Assert.Single(file.Modules, m => m.Title == "开场");
        var firstEffects = Assert.IsType<ExitSyntax>(opening.Exit).Branches[0].Effects;
        var inlineEffect = Assert.Single(firstEffects);
        Assert.Equal("a", inlineEffect.Name);
        Assert.True(inlineEffect.Value);

        var ending = Assert.Single(file.Modules, m => m.Title == "结局");
        var multilineEffects = Assert.IsType<ExitSyntax>(ending.Exit).Branches[0].Effects;
        Assert.Equal(2, multilineEffects.Count);
        Assert.Equal("a", multilineEffects[0].Name);
        Assert.True(multilineEffects[0].Value);
        Assert.Equal("b", multilineEffects[1].Name);
        Assert.True(multilineEffects[1].Value);
    }

    [Fact]
    public void ParsesModuleIdTarget()
    {
        const string source = """
        @title 第20章
        @module 开场
        @next(萧影):[继续] -> 第一章_雨夜
            ()[结束] -> END

        @module 第一章_雨夜
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Empty(result.Diagnostics);
        var file = Assert.IsType<WeftFileSyntax>(result.Script);

        var opening = Assert.Single(file.Modules, m => m.Title == "开场");
        var branch = Assert.IsType<ExitSyntax>(opening.Exit).Branches[0];
        Assert.Equal("第一章_雨夜", branch.Target);
    }

    [Fact]
    public void ReportsMissingFallback()
    {
        const string source = """
        @title 第21章
        @vars
            @var a false
            @var b false

        @module 测试
        @next():(a) -> A
            (b) -> B
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MissingFallback);
    }

    [Fact]
    public void ReportsModeMismatch()
    {
        const string source = """
        @title 第22章
        @module 测试
        @next(萧影):[有文案] -> A
            -> B
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.ModeMismatch);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.InvalidBranchLabel);
    }

    [Fact]
    public void ReportsContentAfterShorthandExit()
    {
        const string source = """
        @title 第23章
        @module 开场
        @next -> END
            [多余内容]
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.ContentAfterExit);
    }

    [Fact]
    public void ReportsMalformedCondition()
    {
        const string source = """
        @title 第24章
        @vars
            @var a false

        @module 测试
        @next():(a and) -> END
            () -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidCondition);
    }

    [Fact]
    public void ReportsUnterminatedEffectBlock()
    {
        const string source = """
        @title 第25章
        @vars
            @var a false

        @module 测试
        @next(萧影):[继续] -> END {
            @set a true
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MalformedExitBranch);
    }

    [Fact]
    public void ReportsLabelInAutoMode()
    {
        const string source = """
        @title 第26章
        @module 测试
        @next():[文案] -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidBranchLabel);
    }

    [Fact]
    public void ReportsEmptyLabelInChoiceMode()
    {
        const string source = """
        @title 第27章
        @module 测试
        @next(萧影):[] -> END
        """;

        var result = WeftlineCompiler.Parse(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidBranchLabel);
    }
}
