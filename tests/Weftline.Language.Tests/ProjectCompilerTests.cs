using Weftline.Language.Diagnostics;
using Weftline.Language.Projects;

namespace Weftline.Language.Tests;

public sealed class ProjectCompilerTests
{
    [Fact]
    public void LoadsBindsValidatesAndBuildsGraph()
    {
        var folder = Directory.CreateTempSubdirectory("weftline-test-");

        try
        {
            System.IO.File.WriteAllText(Path.Combine(folder.FullName, "project.wstory"), """
            @project 雨夜
                @summary [概要。]

                @chars
                    @char 萧影 [主角。]

                @vars
                    @var a false [变量。]
            """);

            System.IO.File.WriteAllText(Path.Combine(folder.FullName, "chapter.weft"), """
            @title 第47章
            @vars
                @var a false

            @chars
                @char 萧影

            @module 开场
                [{萧影}来了。]
                萧影:[你好。]
            @next(萧影):(a)[继续] -> END
                ()[结束] -> END
            """);

            var result = ProjectCompiler.Compile(folder.FullName);

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.Project.WeftFiles);
            Assert.NotNull(result.Graph);
            Assert.NotNull(result.Analysis);
            Assert.Single(result.Graph!.Modules);
            Assert.Equal("雨夜", result.Model.Project.Name);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void DoesNotBuildGraphWhenProjectHasErrors()
    {
        var folder = Directory.CreateTempSubdirectory("weftline-test-");

        try
        {
            System.IO.File.WriteAllText(Path.Combine(folder.FullName, "chapter.weft"), """
            @title 第一章
            @module A
            @next -> END

            @module A
            @next -> END
            """);

            var result = ProjectCompiler.Compile(folder.FullName);

            Assert.True(result.HasErrors);
            Assert.Null(result.Graph);
            Assert.Null(result.Analysis);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReportsDuplicateWStoryChapterWithoutCrashing()
    {
        var folder = Directory.CreateTempSubdirectory("weftline-test-");

        try
        {
            System.IO.File.WriteAllText(Path.Combine(folder.FullName, "project.wstory"), """
            @project 重复章节
                @chapters
                    @chapter 第一章 [一。]
                    @chapter 第一章 [还是第一章。]
            """);

            System.IO.File.WriteAllText(Path.Combine(folder.FullName, "chapter.weft"), """
            @title 第一章
            @module A
            @next -> END
            """);

            var result = ProjectCompiler.Compile(folder.FullName);

            Assert.Contains(
                result.Diagnostics,
                item => item.Diagnostic.Code == DiagnosticCode.DuplicateChapterTitle);
            Assert.Null(result.Graph);
            Assert.Null(result.Analysis);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
