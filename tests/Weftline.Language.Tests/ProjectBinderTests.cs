using Weftline.Language.Diagnostics;
using Weftline.Language.Projects;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class ProjectBinderTests
{
    [Fact]
    public void ResolvesDeclarationsAcrossFiles()
    {
        var declarations = File("decl.weft", """
        @title 第40章
        @vars
            @var 信任 false

        @chars
            @char 萧影

        @scenes
            @scene 医院
        """);

        var story = File("story.weft", """
        @title 第41章
        @module 测试
            @scene 医院
            [{萧影}来了。]
            萧影:[你好。]
        @next(萧影):(信任)[继续] -> END
            ()[结束] -> END
        """);

        var result = ProjectBinder.Bind([declarations, story], null, null);

        Assert.Empty(result.Diagnostics);
        Assert.Contains("萧影", result.Index.Symbols.Keys);
        Assert.Contains("信任", result.Index.Symbols.Keys);
        Assert.Contains("测试", result.Index.Modules.Keys);
    }

    [Fact]
    public void ReportsMissingReferenceAcrossFiles()
    {
        var story = File("story.weft", """
        @title 第42章
        @module 测试
            [{钥匙}。]
        @next -> END
        """);

        var result = ProjectBinder.Bind([story], null, null);

        Assert.Contains(
            result.Diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.UndeclaredReference);
    }

    [Fact]
    public void ReportsDuplicateDeclarationAcrossFiles()
    {
        var first = File("a.weft", """
        @title 第43章
        @vars
            @var a false
        """);

        var second = File("b.weft", """
        @title 第44章
        @vars
            @var a true
        """);

        var result = ProjectBinder.Bind([first, second], null, null);

        var duplicate = Assert.Single(
            result.Diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.DuplicateGlobalVariable);

        Assert.Equal(second.FilePath, duplicate.FilePath);
        Assert.NotNull(duplicate.Related);
        Assert.Equal(first.FilePath, duplicate.Related!.Value.FilePath);
    }

    [Fact]
    public void ReportsDuplicateModuleTitleAcrossFiles()
    {
        var first = File("a.weft", """
        @title 第45章
        @module 同名
        @next -> END
        """);

        var second = File("b.weft", """
        @title 第46章
        @module 同名
        @next -> END
        """);

        var result = ProjectBinder.Bind([first, second], null, null);

        var duplicate = Assert.Single(
            result.Diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.DuplicateModuleTitle);

        Assert.Equal(second.FilePath, duplicate.FilePath);
        Assert.NotNull(duplicate.Related);
        Assert.Equal(first.FilePath, duplicate.Related!.Value.FilePath);
    }

    [Fact]
    public void MapsModulesToChapters()
    {
        var first = File("a.weft", """
        @title 第一章
        @module A
        @next -> END
        """);

        var second = File("b.weft", """
        @title 第二章
        @module B
        @next -> END
        """);

        var result = ProjectBinder.Bind([first, second], null, null);

        Assert.Empty(result.Diagnostics);
        Assert.Equal("第一章", result.Index.Modules["A"].Chapter);
        Assert.Equal("第二章", result.Index.Modules["B"].Chapter);
        Assert.Contains("第一章", result.Index.Chapters.Keys);
        Assert.Contains("第二章", result.Index.Chapters.Keys);
    }

    [Fact]
    public void ReportsDuplicateChapterTitleAcrossFiles()
    {
        var first = File("a.weft", """
        @title 同名
        @module A
        @next -> END
        """);

        var second = File("b.weft", """
        @title 同名
        @module B
        @next -> END
        """);

        var result = ProjectBinder.Bind([first, second], null, null);

        var duplicate = Assert.Single(
            result.Diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.DuplicateChapterTitle);

        Assert.Equal(second.FilePath, duplicate.FilePath);
        Assert.NotNull(duplicate.Related);
        Assert.Equal(first.FilePath, duplicate.Related!.Value.FilePath);
    }

    private static ProjectFile File(string path, string source)
    {
        var result = WeftlineCompiler.Parse(source, path);
        Assert.Empty(result.Diagnostics);
        return new ProjectFile(path, Assert.IsType<WeftFileSyntax>(result.Script));
    }
}
