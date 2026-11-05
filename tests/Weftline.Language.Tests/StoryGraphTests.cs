using Weftline.Language.Ir;
using Weftline.Language.Projects;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class StoryGraphTests
{
    [Fact]
    public void BuildsSemanticGraphFromProjectAst()
    {
        var (project, index) = BuildProject();

        var model = ProjectModelBuilder.Build(project, index);
        var graph = StoryGraphBuilder.Build(project, model);

        Assert.Equal(2, graph.SchemaVersion);
        Assert.Equal("END", graph.End);
        Assert.Equal("开场", graph.Entry);

        var global = Assert.Single(graph.Globals);
        Assert.Equal("a", global.Name);
        Assert.False(global.Initial);

        var module = Assert.Single(graph.Modules);
        Assert.Equal("开场", module.Id);
        Assert.Equal("第48章", module.ChapterId);
        Assert.Equal("医院", module.Scene);
        Assert.Equal("story.weft", module.Source.FilePath);
        Assert.Equal(2, module.Content.Count);

        var narration = module.Content[0];
        Assert.Equal("narration", narration.Kind);
        Assert.Equal("萧影", Assert.Single(narration.Segments!, s => s.Kind == "ref").Name);

        var dialogue = module.Content[1];
        Assert.Equal("dialogue", dialogue.Kind);
        Assert.Equal("萧影", dialogue.Speaker);

        Assert.NotNull(module.Exit);
        Assert.Equal(ExitMode.Choice, module.Exit!.Mode);
        Assert.Equal("萧影", module.Exit.Character);
        Assert.Equal(2, module.Exit.Branches.Count);

        var first = module.Exit.Branches[0];
        Assert.Equal("继续", first.Label);
        Assert.Equal("a", first.Condition?.Name);
        Assert.Equal("END", first.Target);
        Assert.Empty(first.Effects);
        Assert.Equal("story.weft", first.Source.FilePath);

        var second = module.Exit.Branches[1];
        Assert.Equal("结束", second.Label);
        Assert.Null(second.Condition);
    }

    [Fact]
    public void BuildsProjectModelWithDescriptions()
    {
        var (project, index) = BuildProject();

        var model = ProjectModelBuilder.Build(project, index);

        Assert.Equal("雨夜", model.Project.Name);
        Assert.Equal("概要。", model.Project.Summary);
        var doc = Assert.Single(model.Project.Docs);
        Assert.Equal("规则", doc.Title);
        Assert.Equal("文本。", doc.Text);

        var chapter = Assert.Single(model.Chapters);
        Assert.Equal("第48章", chapter.Title);
        Assert.Equal("章节描述。", chapter.Description);
        Assert.Equal("开场", Assert.Single(chapter.ModuleIds));

        var variable = Assert.Single(model.Symbols.Variables);
        Assert.Equal("a", variable.Name);
        Assert.False(variable.Initial);
        Assert.Equal("变量。", variable.Description);

        var character = Assert.Single(model.Symbols.Characters);
        Assert.Equal("萧影", character.Name);
        Assert.Equal("主角。", character.Description);

        var scene = Assert.Single(model.Symbols.Scenes);
        Assert.Equal("医院", scene.Name);
        Assert.Equal("医院。", scene.Description);
    }

    [Fact]
    public void SerializesSemanticGraphToJson()
    {
        var (project, index) = BuildProject();
        var graph = StoryGraphBuilder.Build(project, index);

        var json = StoryGraphJson.Serialize(graph);

        Assert.Contains("\"schemaVersion\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"entry\": \"开场\"", json, StringComparison.Ordinal);
        Assert.Contains("\"id\": \"开场\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"narration\"", json, StringComparison.Ordinal);
        Assert.Contains("\"mode\": \"choice\"", json, StringComparison.Ordinal);
        Assert.Contains("\"globals\": [", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"external\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"chapterEdges\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintsGraphAsText()
    {
        var (project, index) = BuildProject();
        var graph = StoryGraphBuilder.Build(project, index);

        using var writer = new StringWriter();
        StoryGraphPrinter.Print(graph, writer);
        var text = writer.ToString();

        Assert.Contains("StoryGraph schema=2 entry=开场 end=END", text, StringComparison.Ordinal);
        Assert.Contains("Module \"开场\" chapter=\"第48章\" scene=医院", text, StringComparison.Ordinal);
        Assert.Contains("exit: choice character=萧影", text, StringComparison.Ordinal);
        Assert.Contains("edge: [继续] when a -> END", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PreservesCrossChapterTargetsWithoutDerivedEdges()
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

        Assert.Equal(2, graph.Modules.Count);
        Assert.Equal("第一章", graph.Modules[0].ChapterId);
        Assert.Equal("第二章", graph.Modules[1].ChapterId);
        Assert.Equal("B", graph.Modules[0].Exit!.Branches[0].Target);
        Assert.Equal("END", graph.Modules[1].Exit!.Branches[0].Target);
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
