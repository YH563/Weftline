using Weftline.Language.Diagnostics;

namespace Weftline.Language.Tests;

public sealed class BinderTests
{
    [Fact]
    public void ResolvesDeclaredSymbols()
    {
        const string source = """
        @title 第28章
        @vars
            @var a false

        @chars
            @char 萧影

        @items
            @item 钥匙

        @scenes
            @scene 医院

        @module 测试
            @scene 医院
            [{萧影}拿着{钥匙}。]
            萧影:[你好。]
        @next(萧影):(a)[继续] -> END { @set a true }
            ()[结束] -> END
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ReportsUndeclaredCharacterInDialogue()
    {
        const string source = """
        @title 第29章
        @module 测试
            萧影:[你好。]
        @next -> END
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredCharacter);
    }

    [Fact]
    public void ReportsUndeclaredExitCharacter()
    {
        const string source = """
        @title 第30章
        @module 测试
        @next(萧影):[继续] -> END
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredCharacter);
    }

    [Fact]
    public void ReportsUndeclaredTextReference()
    {
        const string source = """
        @title 第31章
        @chars
            @char 萧影

        @module 测试
            [{钥匙}。]
        @next -> END
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredReference);
    }

    [Fact]
    public void ReportsUndeclaredConditionVariable()
    {
        const string source = """
        @title 第32章
        @module 测试
        @next():(missing) -> END
            () -> END
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredConditionVariable);
    }

    [Fact]
    public void ReportsUndeclaredVariableInEffect()
    {
        const string source = """
        @title 第33章
        @module 测试
        @next(萧影):[继续] -> END { @set missing true }
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.UndeclaredVariable);
    }

    [Fact]
    public void ReportsDuplicateWeftDeclarations()
    {
        const string source = """
        @title 第34章
        @vars
            @var a false
            @var a true

        @chars
            @char 萧影
            @char 萧影

        @items
            @item 钥匙
            @item 钥匙

        @scenes
            @scene 医院
            @scene 医院
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.DuplicateGlobalVariable);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.DuplicateCharacter);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.DuplicateItem);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.DuplicateScene);
    }

    [Fact]
    public void ReportsCrossKindDuplicateSymbol()
    {
        const string source = """
        @title 第35章
        @chars
            @char 同名

        @items
            @item 同名
        """;

        var result = WeftlineCompiler.Compile(source, "test.weft");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.DuplicateSymbol);
    }

    [Fact]
    public void ReportsDuplicateWStoryDeclarations()
    {
        const string source = """
        @project 雨夜

        @chars
            @char 萧影
            @char 萧影
        """;

        var result = WeftlineCompiler.Compile(source, "project.wstory");

        Assert.Contains(
            result.Diagnostics,
            d => d.Code == DiagnosticCode.DuplicateCharacter);
    }
}
