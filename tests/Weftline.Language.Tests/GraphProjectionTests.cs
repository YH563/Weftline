using Weftline.Language.Ir;
using Weftline.Language.Projects;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class GraphProjectionTests
{
    [Fact]
    public void BuildsChaptersModulesEdgesAndAggregates()
    {
        var (model, graph) = BuildGraph();

        var projection = GraphProjectionBuilder.Build(model, graph);

        Assert.Equal("A", projection.EntryModuleId);

        var first = Assert.Single(
            projection.Chapters,
            static chapter => chapter.ChapterId == "第一章");
        Assert.Equal(["A", "B"], first.ModuleIds);
        Assert.True(first.Flags.HasFlag(GraphChapterFlags.Entry));
        Assert.True(first.Flags.HasFlag(GraphChapterFlags.HasOutgoing));
        Assert.False(first.Flags.HasFlag(GraphChapterFlags.HasIncoming));
        Assert.False(first.Flags.HasFlag(GraphChapterFlags.HasEndEdge));

        var second = Assert.Single(
            projection.Chapters,
            static chapter => chapter.ChapterId == "第二章");
        Assert.Equal(["C", "D"], second.ModuleIds);
        Assert.True(second.Flags.HasFlag(GraphChapterFlags.HasIncoming));
        Assert.False(second.Flags.HasFlag(GraphChapterFlags.HasOutgoing));
        Assert.True(second.Flags.HasFlag(GraphChapterFlags.HasEndEdge));

        var moduleA = Assert.Single(
            projection.Modules,
            static module => module.ModuleId == "A");
        Assert.True(moduleA.Flags.HasFlag(GraphModuleFlags.Entry));
        Assert.False(moduleA.Flags.HasFlag(GraphModuleFlags.TargetsEnd));
        Assert.Equal(2, moduleA.BranchCount);
        Assert.Equal("小明在房间。", moduleA.Summary.FirstLine);
        Assert.Equal(1, moduleA.Summary.DialogueCount);
        Assert.Equal(0, moduleA.Summary.ChoiceCount);

        var moduleC = Assert.Single(
            projection.Modules,
            static module => module.ModuleId == "C");
        Assert.True(moduleC.Flags.HasFlag(GraphModuleFlags.TargetsEnd));

        Assert.Equal(2, projection.ModuleEdges.Count(
            static edge => edge.FromModule == "A"));
        Assert.All(
            projection.ModuleEdges.Where(static edge => edge.FromModule == "A"),
            static edge => Assert.False(edge.External));

        Assert.Equal(3, projection.ModuleEdges.Count(
            static edge => edge.FromModule == "B"));
        Assert.All(
            projection.ModuleEdges.Where(static edge => edge.FromModule == "B"),
            static edge => Assert.True(edge.External));

        Assert.Equal(2, projection.ChapterEdges.Count);
    }

    [Fact]
    public void MarksUnreachableAndCannotReachEndModules()
    {
        var file = ParseWeft("chapter.weft", """
        @title 第一章
        @module A
        @next -> END

        @module B
        @next -> B
        """);

        var (model, graph) = BuildModelAndGraph([file]);
        var projection = GraphProjectionBuilder.Build(model, graph);

        var moduleA = Assert.Single(
            projection.Modules,
            static module => module.ModuleId == "A");
        Assert.True(moduleA.Flags.HasFlag(GraphModuleFlags.Entry));
        Assert.True(moduleA.Flags.HasFlag(GraphModuleFlags.TargetsEnd));
        Assert.False(moduleA.Flags.HasFlag(GraphModuleFlags.Unreachable));
        Assert.False(moduleA.Flags.HasFlag(GraphModuleFlags.CannotReachEnd));

        var moduleB = Assert.Single(
            projection.Modules,
            static module => module.ModuleId == "B");
        Assert.True(moduleB.Flags.HasFlag(GraphModuleFlags.Unreachable));
        Assert.True(moduleB.Flags.HasFlag(GraphModuleFlags.CannotReachEnd));
    }

    private static (ProjectModel Model, StoryGraph Graph) BuildGraph()
    {
        var first = ParseWeft("chapter1.weft", """
        @title 第一章
        @vars
            @var x false

        @chars
            @char 小明

        @scenes
            @scene 房间

        @module A
            @scene 房间
            [小明在房间。]
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

        return BuildModelAndGraph([first, second]);
    }

    private static (ProjectModel Model, StoryGraph Graph) BuildModelAndGraph(
        IReadOnlyList<ProjectFile> files)
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
