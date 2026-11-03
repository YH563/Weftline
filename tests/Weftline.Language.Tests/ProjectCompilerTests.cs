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
            Assert.Single(result.Graph.Modules);
            Assert.Equal("雨夜", result.Graph.Project?.Name);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
