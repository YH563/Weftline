using Weftline.Language.Generation;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class WStoryGeneratorTests
{
    [Fact]
    public void GeneratesFromWeftAndPreservesDescriptions()
    {
        const string weftSource = """
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
            [正文。]
        @next -> END
        """;

        const string existingSource = """
        @project 雨夜
            @summary [一个关于信任的故事。]

            @chars
                @char 萧影 [主角。]

            @vars
                @var trust_low false [萧影是否信任影子。]
        """;

        var weft = Assert.IsType<WeftFileSyntax>(
            WeftlineCompiler.Parse(weftSource, "chapter1.weft").Script);
        var existing = Assert.IsType<WStoryFileSyntax>(
            WeftlineCompiler.Parse(existingSource, "project.wstory").Script);

        var generated = WStoryGenerator.Generate([weft], existing, "fallback");

        Assert.Contains("@project 雨夜", generated, StringComparison.Ordinal);
        Assert.Contains("@char 萧影 [主角。]", generated, StringComparison.Ordinal);
        Assert.Contains("@char 影子 []", generated, StringComparison.Ordinal);
        Assert.Contains("@var trust_low false [萧影是否信任影子。]", generated, StringComparison.Ordinal);
        Assert.Contains("@var has_key false []", generated, StringComparison.Ordinal);

        var reparsed = WeftlineCompiler.Parse(generated, "project.wstory");
        Assert.Empty(reparsed.Diagnostics);
        Assert.IsType<WStoryFileSyntax>(reparsed.Script);
    }

    [Fact]
    public void GenerationIsIdempotent()
    {
        const string weftSource = """
        @vars
            @var a false

        @chars
            @char 萧影
        """;

        const string existingSource = """
        @project 雨夜
            @chars
                @char 萧影 [主角。]
        """;

        var weft = Assert.IsType<WeftFileSyntax>(
            WeftlineCompiler.Parse(weftSource, "chapter1.weft").Script);
        var existing = Assert.IsType<WStoryFileSyntax>(
            WeftlineCompiler.Parse(existingSource, "project.wstory").Script);

        var first = WStoryGenerator.Generate([weft], existing, "fallback");
        var regenerated = Assert.IsType<WStoryFileSyntax>(
            WeftlineCompiler.Parse(first, "project.wstory").Script);
        var second = WStoryGenerator.Generate([weft], regenerated, "fallback");

        Assert.Equal(first, second);
    }

    [Fact]
    public void PreservesSummaryAndDocs()
    {
        const string weftSource = """
        @chars
            @char 萧影
        """;

        const string existingSource = """
        @project 雨夜
            @summary [一个故事。]

            @docs
                @doc 世界规则 [医院不关门。]
        """;

        var weft = Assert.IsType<WeftFileSyntax>(
            WeftlineCompiler.Parse(weftSource, "chapter1.weft").Script);
        var existing = Assert.IsType<WStoryFileSyntax>(
            WeftlineCompiler.Parse(existingSource, "project.wstory").Script);

        var generated = WStoryGenerator.Generate([weft], existing, "fallback");

        Assert.Contains("@summary [一个故事。]", generated, StringComparison.Ordinal);
        Assert.Contains("@doc 世界规则 [医院不关门。]", generated, StringComparison.Ordinal);
    }
}
