using Weftline.Language.Ir;
using Weftline.Language.Projects;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class GraphAnalysisTests
{
    [Fact]
    public void BuildsReachabilityReferencesPathsAndChapterEdges()
    {
        var (model, graph) = BuildGraph();

        var analysis = GraphAnalysisBuilder.Build(model, graph);

        Assert.Contains("A", analysis.Reachable);
        Assert.Contains("B", analysis.Reachable);
        Assert.Contains("C", analysis.Reachable);
        Assert.Contains("D", analysis.Reachable);

        Assert.Contains("A", analysis.CanReachEnd);
        Assert.Contains("B", analysis.CanReachEnd);
        Assert.Contains("C", analysis.CanReachEnd);
        Assert.Contains("D", analysis.CanReachEnd);

        Assert.Equal(["A"], analysis.References.Characters["小明"]);
        Assert.Equal(["A"], analysis.References.Items["钥匙"]);
        Assert.Equal(["A"], analysis.References.Scenes["房间"]);
        Assert.Equal(["A", "B"], analysis.References.Variables["x"]);

        var chapterEdge = Assert.Single(
            analysis.ChapterEdges,
            edge => edge.FromChapter == "第一章" && edge.ToChapter == "第二章");
        Assert.Equal(3, chapterEdge.Count);
        Assert.Equal(["C", "C", "D"], chapterEdge.Details.Select(static detail => detail.ToModule));
        Assert.All(chapterEdge.Details, static detail => Assert.Equal(ExitMode.Auto, detail.Mode));

        var endEdge = Assert.Single(
            analysis.ChapterEdges,
            edge => edge.ToChapter == graph.End);
        Assert.Equal("第二章", endEdge.FromChapter);
        Assert.Equal(2, endEdge.Count);

        var paths = analysis.FindPaths("A", graph.End);
        Assert.Equal(2, paths.Count);
        Assert.All(paths, static path => Assert.Equal("A", path[0]));
        Assert.All(paths, static path => Assert.Equal("END", path[^1]));
    }

    [Fact]
    public void ReportsUnreachableAndCannotReachEndSets()
    {
        var (model, graph) = BuildUnreachableGraph();

        var analysis = GraphAnalysisBuilder.Build(model, graph);

        Assert.Contains("A", analysis.Reachable);
        Assert.DoesNotContain("B", analysis.Reachable);
        Assert.Contains("A", analysis.CanReachEnd);
        Assert.DoesNotContain("B", analysis.CanReachEnd);
    }

    private static (ProjectModel Model, StoryGraph Graph) BuildGraph()
    {
        var first = ParseWeft("chapter1.weft", """
        @title 第一章
        @vars
            @var x false

        @chars
            @char 小明

        @items
            @item 钥匙

        @scenes
            @scene 房间

        @module A
            @scene 房间
            [小明拿着{钥匙}在房间。]
            小明:[你好。]
        @next():(x) -> B
            () -> B

        @module B
            [B。]
        @next():(x) -> C
            () -> C
            () -> D
        """);

        var second = ParseWeft("chapter2.weft", """
        @title 第二章
        @module C
            [C。]
        @next -> END

        @module D
            [D。]
        @next -> END
        """);

        return BuildModelAndGraph(first, second);
    }

    private static (ProjectModel Model, StoryGraph Graph) BuildUnreachableGraph()
    {
        var file = ParseWeft("chapter.weft", """
        @title 第一章
        @module A
        @next -> END

        @module B
        @next -> B
        """);

        return BuildModelAndGraph(file);
    }

    private static (ProjectModel Model, StoryGraph Graph) BuildModelAndGraph(params ProjectFile[] files)
    {
        var project = new LoadedProject(
            "test",
            null,
            null,
            files,
            Array.Empty<ProjectDiagnostic>());

        var binding = ProjectBinder.Bind(files, null, null);
        Assert.Empty(binding.Diagnostics);

        var model = ProjectModelBuilder.Build(project, binding.Index);
        var graph = StoryGraphBuilder.Build(project, model);

        return (model, graph);
    }

    private static ProjectFile ParseWeft(string path, string source)
    {
        var parse = WeftlineCompiler.Parse(source, path);
        Assert.Empty(parse.Diagnostics);
        return new ProjectFile(path, Assert.IsType<WeftFileSyntax>(parse.Script));
    }
}
