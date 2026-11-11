using Weftline.Graph.Analysis;
using Weftline.Models;

namespace Weftline.Graph.Tests;

public sealed class GraphAnalysisTests
{
    private static readonly SourceLocation Source = new("test.weft", new TextSpan(0, 0));

    [Fact]
    public void PathHighlightUsesEntryAndExitShortestPaths()
    {
        var projection = CreateProjection(
            modules: ["A", "B", "C", "D"],
            edges: [("A", "B"), ("B", "C"), ("B", "D"), ("C", "END"), ("D", "D")],
            chapterEdges: [("第一章", "END", [("C", "END")])]);

        var highlight = GraphPathAnalyzer.Build(projection, "B");

        Assert.Equal(["A", "B", "C"], highlight.Modules.Order());
        Assert.Contains(("A", "B"), highlight.ModuleEdges);
        Assert.Contains(("B", "C"), highlight.ModuleEdges);
        Assert.Contains(("C", "END"), highlight.ModuleEdges);
        Assert.DoesNotContain(("B", "D"), highlight.ModuleEdges);
        Assert.Contains(("第一章", "END"), highlight.ChapterEdges);
    }

    [Fact]
    public void PathHighlightHandlesCycles()
    {
        var projection = CreateProjection(
            modules: ["A", "B"],
            edges: [("A", "B"), ("B", "A"), ("B", "END")]);

        var highlight = GraphPathAnalyzer.Build(projection, "B");

        Assert.Equal(["A", "B"], highlight.Modules.Order());
        Assert.Contains(("A", "B"), highlight.ModuleEdges);
        Assert.Contains(("B", "END"), highlight.ModuleEdges);
    }

    [Fact]
    public void FilterMatchesCharactersScenesAndVariables()
    {
        var projection = CreateProjection(
            modules: ["A", "B"],
            edges: [("A", "B"), ("B", "END")],
            characters: new()
            {
                ["甲"] = ["A"],
                ["乙"] = ["B"],
            },
            variables: new()
            {
                ["x"] = ["A", "B"],
            },
            scenes: new()
            {
                ["房间"] = ["A"],
                ["街道"] = ["B"],
            });

        var character = GraphFilterMatcher.Match(
            projection,
            new GraphFilterSelection(
                new HashSet<string>(["乙"], StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)));

        Assert.Equal(["B"], character.Modules);
        Assert.Contains(("B", "END"), character.ModuleEdges);
        Assert.DoesNotContain(("A", "B"), character.ModuleEdges);

        var variable = GraphFilterMatcher.Match(
            projection,
            new GraphFilterSelection(
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(["x"], StringComparer.Ordinal)));

        Assert.Equal(["A", "B"], variable.Modules.Order());
        Assert.Contains(("A", "B"), variable.ModuleEdges);

        var combined = GraphFilterMatcher.Match(
            projection,
            new GraphFilterSelection(
                new HashSet<string>(["乙"], StringComparer.Ordinal),
                new HashSet<string>(["房间"], StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)));

        Assert.Empty(combined.Modules);
    }

    private static GraphProjection CreateProjection(
        string[] modules,
        (string From, string To)[] edges,
        (string From, string To, (string From, string To)[] Details)[]? chapterEdges = null,
        Dictionary<string, List<string>>? characters = null,
        Dictionary<string, List<string>>? variables = null,
        Dictionary<string, List<string>>? scenes = null)
    {
        var nodes = modules.Select(moduleId => new GraphModuleNode(
            moduleId,
            "第一章",
            null,
            1,
            moduleId == "A" ? GraphModuleFlags.Entry : GraphModuleFlags.None,
            new GraphModuleSummary(null, 0, 0),
            Source)).ToList();

        var moduleEdges = edges.Select(edge => new GraphModuleEdge(
            edge.From,
            edge.To,
            ExitMode.Auto,
            null,
            null,
            false,
            Source)).ToList();

        var chapterEdgeList = (chapterEdges ?? [])
            .Select(edge => new ChapterEdge(
                edge.From,
                edge.To,
                edge.Details.Select(detail => new ChapterEdgeDetail(
                    detail.From,
                    detail.To,
                    ExitMode.Auto,
                    null,
                    null,
                    Source)).ToList()))
            .ToList();

        return new GraphProjection(
            "A",
            [new GraphChapter("第一章", "第一章", modules, GraphChapterFlags.Entry)],
            nodes,
            moduleEdges,
            chapterEdgeList,
            new GraphReferenceIndex(
                ToReadOnly(characters),
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                ToReadOnly(scenes),
                ToReadOnly(variables)));
    }

    private static Dictionary<string, IReadOnlyList<string>> ToReadOnly(
        Dictionary<string, List<string>>? source)
        => source is null
            ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            : source.ToDictionary(
                static pair => pair.Key,
                static pair => (IReadOnlyList<string>)pair.Value,
                StringComparer.Ordinal);
}
