using Weftline.Core.Graph.Analysis;
using Weftline.Core;

namespace Weftline.Core.Tests;

public sealed class GraphAnalysisTests
{
    private static readonly SourceLocation Source = new("test.weft", new TextSpan(0, 0));

    [Fact]
    public void PathHighlightIncludesSelectedModuleAndDirectEdges()
    {
        var projection = CreateProjection(
            modules: ["A", "B", "C", "D"],
            edges: [("A", "B"), ("B", "C"), ("B", "D"), ("C", "END"), ("D", "D")],
            chapterEdges: [("第一章", "END", [("C", "END")])]);

        var highlight = GraphPathAnalyzer.Build(projection, "B");

        Assert.Equal(["B"], highlight.Modules.Order());
        Assert.Contains(("A", "B"), highlight.ModuleEdges);
        Assert.Contains(("B", "C"), highlight.ModuleEdges);
        Assert.Contains(("B", "D"), highlight.ModuleEdges);
        Assert.DoesNotContain(("C", "END"), highlight.ModuleEdges);
        Assert.DoesNotContain(("D", "D"), highlight.ModuleEdges);
        Assert.Empty(highlight.ChapterEdges);
    }

    [Fact]
    public void PathHighlightIncludesOnlyDirectIncomingAndOutgoingEdges()
    {
        var projection = CreateProjection(
            modules: ["A", "B", "C"],
            edges:
            [
                ("A", "B"),
                ("A", "C"),
                ("B", "C"),
                ("B", "END"),
                ("C", "END"),
            ]);

        var highlight = GraphPathAnalyzer.Build(projection, "C");

        Assert.Equal(["C"], highlight.Modules.Order());
        Assert.Contains(("B", "C"), highlight.ModuleEdges);
        Assert.Contains(("C", "END"), highlight.ModuleEdges);
        Assert.DoesNotContain(("A", "B"), highlight.ModuleEdges);
    }

    [Fact]
    public void PathHighlightForChapterIncludesRelatedPaths()
    {
        var projection = CreateProjection(
            modules: ["A", "B", "C"],
            edges: [("A", "B"), ("B", "C"), ("C", "END")],
            chapterEdges:
            [
                ("第一章", "第二章", [("B", "C")]),
                ("第二章", "END", [("C", "END")]),
            ],
            chapters: [("第一章", ["A", "B"]), ("第二章", ["C"])]);

        var highlight = GraphPathAnalyzer.Build(projection, null, "第一章");

        Assert.Equal(["A", "B"], highlight.Modules.Order());
        Assert.Contains(("A", "B"), highlight.ModuleEdges);
        Assert.Contains(("B", "C"), highlight.ModuleEdges);
        Assert.DoesNotContain(("C", "END"), highlight.ModuleEdges);
        Assert.Contains(("第一章", "第二章"), highlight.ChapterEdges);
        Assert.DoesNotContain(("第二章", "END"), highlight.ChapterEdges);
    }

    [Fact]
    public void PathHighlightFallsBackToEntryCandidates()
    {
        var projection = CreateProjection(
            modules: ["A", "B", "C"],
            edges: [("B", "C"), ("C", "END")],
            entryCandidates: ["B"]);

        var highlight = GraphPathAnalyzer.Build(projection, "C");

        Assert.Equal(["C"], highlight.Modules.Order());
        Assert.Contains(("B", "C"), highlight.ModuleEdges);
        Assert.Contains(("C", "END"), highlight.ModuleEdges);
    }

    [Fact]
    public void PathHighlightHandlesCycles()
    {
        var projection = CreateProjection(
            modules: ["A", "B"],
            edges: [("A", "B"), ("B", "A"), ("B", "END")]);

        var highlight = GraphPathAnalyzer.Build(projection, "B");

        Assert.Equal(["B"], highlight.Modules.Order());
        Assert.Contains(("A", "B"), highlight.ModuleEdges);
        Assert.Contains(("B", "A"), highlight.ModuleEdges);
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

    [Fact]
    public void FilterMatchesAnySceneInModule()
    {
        var projection = CreateProjection(
            modules: ["A"],
            edges: [("A", "END")],
            scenes: new()
            {
                ["街道"] = ["A"],
                ["医院"] = ["A"],
            });

        var match = GraphFilterMatcher.Match(
            projection,
            new GraphFilterSelection(
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(["医院"], StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)));

        Assert.Equal(["A"], match.Modules);
    }

    private static GraphProjection CreateProjection(
        string[] modules,
        (string From, string To)[] edges,
        (string From, string To, (string From, string To)[] Details)[]? chapterEdges = null,
        Dictionary<string, List<string>>? characters = null,
        Dictionary<string, List<string>>? variables = null,
        Dictionary<string, List<string>>? scenes = null,
        string[]? entryCandidates = null,
        (string ChapterId, string[] Modules)[]? chapters = null)
    {
        var chapterEntries = chapters is null
            ? [new GraphChapter("第一章", "第一章", modules, GraphChapterFlags.Entry)]
            : chapters
                .Select((chapter, index) => new GraphChapter(
                    chapter.ChapterId,
                    chapter.ChapterId,
                    chapter.Modules,
                    index == 0 ? GraphChapterFlags.Entry : GraphChapterFlags.None))
                .ToList();

        string ChapterIdFor(string moduleId)
            => chapters?
                .FirstOrDefault(chapter =>
                    chapter.Modules.Contains(moduleId, StringComparer.Ordinal))
                .ChapterId
                ?? "第一章";

        var nodes = modules.Select(moduleId => new GraphModuleNode(
            moduleId,
            ChapterIdFor(moduleId),
            scenes?
                .Where(pair => pair.Value.Contains(moduleId, StringComparer.Ordinal))
                .Select(static pair => pair.Key)
                .ToList() ?? [],
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
            entryCandidates ?? [],
            chapterEntries,
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
