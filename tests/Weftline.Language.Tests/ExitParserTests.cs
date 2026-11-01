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
        @globals
        @var has_key false

        @title 医院
        @char 萧影

        @next():(has_key) -> 地下层
            () -> 走廊

        @title 地下层
        @char 萧影
        @next -> END

        @title 走廊
        @char 萧影
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Empty(result.Diagnostics);

        var hospital = Assert.Single(result.Script.Modules, m => m.Title == "医院");
        var exit = Assert.IsType<ExitSyntax>(hospital.Exit);

        Assert.Null(exit.Character);
        Assert.False(exit.IsChoiceMode);
        Assert.Equal(2, exit.Branches.Count);

        var first = exit.Branches[0];
        var condition = Assert.IsType<BoolRefSyntax>(first.Condition);
        Assert.Equal("has_key", condition.Name);
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
        @title 开场
        @char 萧影
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Empty(result.Diagnostics);

        var module = Assert.Single(result.Script.Modules);
        var exit = Assert.IsType<ExitSyntax>(module.Exit);

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
    public void ParsesConditionPrecedence()
    {
        const string source = """
        @globals
        @var a false
        @var b false
        @var c false

        @title 测试
        @char 萧影

        @next():(a and not b or c) -> 结束
            () -> END

        @title 结束
        @char 萧影
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Empty(result.Diagnostics);

        var module = Assert.Single(result.Script.Modules, m => m.Title == "测试");
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
        @globals
        @var a false
        @var b false

        @title 开场
        @char 萧影

        @next(萧影):[继续] -> 结局 { @set a true }
            ()[结束] -> END

        @title 结局
        @char 萧影

        @next(萧影):[继续] -> END {
            @set a true
            @set b true
        }
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Empty(result.Diagnostics);

        var opening = Assert.Single(result.Script.Modules, m => m.Title == "开场");
        var firstEffects = Assert.IsType<ExitSyntax>(opening.Exit).Branches[0].Effects;
        var inlineEffect = Assert.Single(firstEffects);
        Assert.Equal("a", inlineEffect.Name);
        Assert.True(inlineEffect.Value);

        var ending = Assert.Single(result.Script.Modules, m => m.Title == "结局");
        var multilineEffects = Assert.IsType<ExitSyntax>(ending.Exit).Branches[0].Effects;
        Assert.Equal(2, multilineEffects.Count);
        Assert.Equal("a", multilineEffects[0].Name);
        Assert.True(multilineEffects[0].Value);
        Assert.Equal("b", multilineEffects[1].Name);
        Assert.True(multilineEffects[1].Value);
    }

    [Fact]
    public void ParsesTargetWithSpaces()
    {
        const string source = """
        @title 开场
        @char 萧影

        @next(萧影):[继续] -> 第一章 雨夜
            ()[结束] -> END

        @title 第一章 雨夜
        @char 萧影
        @next -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Empty(result.Diagnostics);

        var opening = Assert.Single(result.Script.Modules, m => m.Title == "开场");
        var branch = Assert.IsType<ExitSyntax>(opening.Exit).Branches[0];
        Assert.Equal("第一章 雨夜", branch.Target);
    }

    [Fact]
    public void ReportsMissingFallback()
    {
        const string source = """
        @globals
        @var a false
        @var b false

        @title 测试
        @char 萧影

        @next():(a) -> A
            (b) -> B
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MissingFallback);
    }

    [Fact]
    public void ReportsModeMismatch()
    {
        const string source = """
        @title 测试
        @char 萧影

        @next(萧影):[有文案] -> A
            -> B
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.ModeMismatch);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidBranchLabel);
    }

    [Fact]
    public void ReportsContentAfterShorthandExit()
    {
        const string source = """
        @title 开场
        @char 萧影
        @next -> END
        [多余内容]
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.ContentAfterExit);
    }

    [Fact]
    public void ReportsMalformedCondition()
    {
        const string source = """
        @globals
        @var a false

        @title 测试
        @char 萧影

        @next():(a and) -> END
            () -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidCondition);
    }

    [Fact]
    public void ReportsUndeclaredConditionVariable()
    {
        const string source = """
        @title 测试
        @char 萧影

        @next():(missing) -> END
            () -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredConditionVariable);
    }

    [Fact]
    public void ReportsUndeclaredVariableInEffect()
    {
        const string source = """
        @title 测试
        @char 萧影

        @next(萧影):[继续] -> END { @set missing true }
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredVariable);
    }

    [Fact]
    public void ReportsUnterminatedEffectBlock()
    {
        const string source = """
        @globals
        @var a false

        @title 测试
        @char 萧影

        @next(萧影):[继续] -> END {
            @set a true
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.MalformedExitBranch);
    }

    [Fact]
    public void ReportsLabelInAutoMode()
    {
        const string source = """
        @title 测试
        @char 萧影
        @next():[文案] -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidBranchLabel);
    }

    [Fact]
    public void ReportsEmptyLabelInChoiceMode()
    {
        const string source = """
        @title 测试
        @char 萧影
        @next(萧影):[] -> END
        """;

        var result = WeftlineCompiler.Parse(source);

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.InvalidBranchLabel);
    }
}
