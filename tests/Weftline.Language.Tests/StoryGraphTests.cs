using Weftline.Language.Ir;
using Weftline.Language.Projects;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class StoryGraphTests
{
    [Fact]
    public void BuildsGraphFromProjectAst()
    {
        var (project, index) = BuildProject();

        var graph = StoryGraphBuilder.Build(project, index);

        Assert.Equal(1, graph.SchemaVersion);
        Assert.Equal("END", graph.End);
        Assert.Equal("开场", graph.Entry);
        Assert.Equal("雨夜", graph.Project?.Name);
        Assert.Equal("概要。", graph.Project?.Summary);
        var doc = Assert.Single(graph.Project!.Docs);
        Assert.Equal("规则", doc.Title);
        Assert.Equal("文本。", doc.Text);

        Assert.False(graph.Globals["a"]);

        var chapter = Assert.Single(graph.Chapters);
        Assert.Equal("第48章", chapter.Title);
        Assert.Equal("章节描述。", chapter.Description);
        Assert.Equal("开场", Assert.Single(chapter.Modules));
        Assert.Empty(graph.ChapterEdges);

        var module = Assert.Single(graph.Modules);
        Assert.Equal("开场", module.Id);
        Assert.Equal("第48章", module.Chapter);
        Assert.Equal("医院", module.Scene);
        Assert.Equal(2, module.Content.Count);

        var narration = Assert.IsType<IrContentBlock>(module.Content[0]);
        Assert.Equal("narration", narration.Kind);
        Assert.Equal("萧影", Assert.Single(narration.Segments!.OfType<IrTextSegment>(), s => s.Kind == "ref").Name);

        var dialogue = module.Content[1];
        Assert.Equal("dialogue", dialogue.Kind);
        Assert.Equal("萧影", dialogue.Speaker);

        Assert.Equal(2, module.Exits.Count);
        var first = module.Exits[0];
        Assert.Equal("萧影", first.Character);
        Assert.Equal("继续", first.Label);
        Assert.Equal("END", first.Target);
        Assert.Equal("a", first.Condition?.Name);
        Assert.Empty(first.Effects);

        var second = module.Exits[1];
        Assert.Equal("结束", second.Label);
        Assert.Null(second.Condition);
    }

    [Fact]
    public void SerializesGraphToJson()
    {
        var (project, index) = BuildProject();
        var graph = StoryGraphBuilder.Build(project, index);

        var json = StoryGraphJson.Serialize(graph);

        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"entry\": \"开场\"", json, StringComparison.Ordinal);
        Assert.Contains("\"id\": \"开场\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"narration\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintsGraphAsText()
    {
        var (project, index) = BuildProject();
        var graph = StoryGraphBuilder.Build(project, index);

        using var writer = new StringWriter();
        StoryGraphPrinter.Print(graph, writer);
        var text = writer.ToString();

        Assert.Contains("StoryGraph schema=1 entry=开场 end=END", text, StringComparison.Ordinal);
        Assert.Contains("Module \"开场\" chapter=\"第48章\" scene=医院", text, StringComparison.Ordinal);
        Assert.Contains("edge: [继续] when a -> END", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildsChaptersAndExternalEdges()
    {
        var first = ParseWeft("chapter1.weft", """
        @title 第一章
        @module A
            [A。]
        @next -> B
        """);

        var second = ParseWeft("chapter2.weft", """
        @title 第二章
        @module B
            [B。]
        @next -> END
        """);

        var project = new LoadedProject(
            "test",
            null,
            null,
            [first, second],
            Array.Empty<ProjectDiagnostic>());

        var binding = ProjectBinder.Bind([first, second], null, null);
        Assert.Empty(binding.Diagnostics);

        var graph = StoryGraphBuilder.Build(project, binding.Index);

        Assert.Equal(2, graph.Chapters.Count);
        Assert.Equal("第一章", graph.Modules[0].Chapter);
        Assert.Equal("第二章", graph.Modules[1].Chapter);
        Assert.True(graph.Modules[0].Exits[0].External);
        Assert.False(graph.Modules[1].Exits[0].External);

        var chapterEdge = Assert.Single(graph.ChapterEdges);
        Assert.Equal("第一章", chapterEdge.From);
        Assert.Equal("第二章", chapterEdge.To);
        Assert.Equal("A -> B", Assert.Single(chapterEdge.Via));
    }

    private static ProjectFile ParseWeft(string path, string source)
    {
        var parse = WeftlineCompiler.Parse(source, path);
        Assert.Empty(parse.Diagnostics);
        return new ProjectFile(
            path,
            Assert.IsType<WeftFileSyntax>(parse.Script));
    }

    private static (LoadedProject Project, ProjectIndex Index) BuildProject()
    {
        const string weftSource = """
        @title 第48章
        @vars
            @var a false

        @chars
            @char 萧影

        @scenes
            @scene 医院

        @module 开场
            @scene 医院
            [{萧影}来了。]
            萧影:[你好。]
        @next(萧影):(a)[继续] -> END
            ()[结束] -> END
        """;

        const string wstorySource = """
        @project 雨夜
            @summary [概要。]

            @chapters
                @chapter 第48章 [章节描述。]

            @docs
                @doc 规则 [文本。]

            @chars
                @char 萧影 [主角。]

            @scenes
                @scene 医院 [医院。]

            @vars
                @var a false [变量。]
        """;

        var weftParse = WeftlineCompiler.Parse(weftSource, "story.weft");
        Assert.Empty(weftParse.Diagnostics);
        var wstoryParse = WeftlineCompiler.Parse(wstorySource, "project.wstory");
        Assert.Empty(wstoryParse.Diagnostics);

        var file = new ProjectFile(
            "story.weft",
            Assert.IsType<WeftFileSyntax>(weftParse.Script));
        var wstory = Assert.IsType<WStoryFileSyntax>(wstoryParse.Script);

        var project = new LoadedProject(
            "test",
            wstory,
            "project.wstory",
            [file],
            Array.Empty<ProjectDiagnostic>());

        var binding = ProjectBinder.Bind([file], wstory, "project.wstory");
        Assert.Empty(binding.Diagnostics);

        return (project, binding.Index);
    }
}
