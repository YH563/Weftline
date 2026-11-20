using Weftline.Core;
using Weftline.Core.Language;

namespace Weftline.Core.Tests;

public sealed class EditorLanguageServiceTests
{
    [Fact]
    public async Task ContentBracesCompleteCharactersAndItems()
    {
        var service = CreateService();

        var items = await CompleteAsync(service, "chapter.weft", "  正文 {");

        Assert.Contains("萧影", items);
        Assert.Contains("钥匙", items);
        Assert.DoesNotContain("医院", items);
        Assert.DoesNotContain("trust", items);
    }

    [Fact]
    public async Task SceneDirectiveCompletesScenes()
    {
        var service = CreateService();

        var items = await CompleteAsync(service, "chapter.weft", "@scene ");

        Assert.Contains("医院", items);
        Assert.DoesNotContain("trust", items);
    }

    [Fact]
    public async Task ConditionCompletesVariablesOnly()
    {
        var service = CreateService();

        var items = await CompleteAsync(service, "chapter.weft", "@next(): (");

        Assert.Contains("trust", items);
        Assert.DoesNotContain("萧影", items);
        Assert.DoesNotContain("钥匙", items);
        Assert.DoesNotContain("医院", items);
    }

    [Fact]
    public async Task EntryCompletesModules()
    {
        var service = CreateService();

        var items = await CompleteAsync(service, "project.wstory", "@entry ");

        Assert.Contains("序", items);
        Assert.DoesNotContain("END", items);
    }

    [Fact]
    public async Task WStoryDirectiveCompletionIncludesEntry()
    {
        var service = CreateService();

        var items = await CompleteAsync(service, "project.wstory", "@");

        Assert.Contains("@entry", items);
    }

    [Fact]
    public async Task EntryHoverResolvesModule()
    {
        var service = CreateService();

        var hover = await service.GetHoverAsync(
            new WeftHoverContext("project.wstory", "@entry 序", "@entry 序".Length),
            CancellationToken.None);

        Assert.NotNull(hover);
        Assert.Equal("模块 序", hover!.Title);
    }

    private static async Task<IReadOnlyList<string>> CompleteAsync(
        EditorLanguageService service,
        string filePath,
        string text)
    {
        var offset = text.Length;
        var wordStart = text.EndsWith('@') ? offset - 1 : offset;
        var result = await service.GetCompletionsAsync(
            new WeftCompletionContext(filePath, text, offset, wordStart),
            CancellationToken.None);

        return result.Items.Select(static item => item.Text).ToList();
    }

    private static EditorLanguageService CreateService()
    {
        var source = new SourceLocation("test.weft", new TextSpan(0, 0));
        var service = new EditorLanguageService(new EmptyParser())
        {
            Symbols = new WeftProjectSymbols(
                [new WeftSymbolInfo("trust", false, null, source)],
                [new WeftSymbolInfo("萧影", null, null, source)],
                [new WeftSymbolInfo("钥匙", null, null, source)],
                [new WeftSymbolInfo("医院", null, null, source)],
                []),
            Projection = new GraphProjection(
                null,
                [],
                [],
                [
                    new GraphModuleNode(
                        "序",
                        "第一章",
                        [],
                        0,
                        GraphModuleFlags.None,
                        new GraphModuleSummary(null, 0, 0),
                        source),
                ],
                [],
                [],
                GraphReferenceIndex.Empty),
        };
        return service;
    }

    private sealed class EmptyParser : IWeftSyntaxParser
    {
        public ValueTask<IReadOnlyList<WeftDiagnostic>> ParseAsync(
            string filePath,
            string text,
            long version,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<WeftDiagnostic>>([]);
    }
}
