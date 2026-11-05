using Weftline.Language.Projects;

namespace Weftline.Language.Tests;

public sealed class ProjectSnapshotTests
{
    [Fact]
    public void CompilesOpenDocumentTextInsteadOfDiskContents()
    {
        var folder = Directory.CreateTempSubdirectory("weftline-test-");

        try
        {
            var path = Path.Combine(folder.FullName, "chapter.weft");
            File.WriteAllText(path, """
            @title 磁盘
            @module 旧模块
            @next -> END
            """);

            var snapshot = new ProjectSnapshot(
                folder.FullName,
                1,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [path] = """
                    @title 内存
                    @module 新模块
                    @next -> END
                    """,
                });

            var result = ProjectCompiler.Compile(
                snapshot,
                cache: null,
                options: new CompilationOptions(AllowPartial: true, BuildProjection: true));

            Assert.Equal("内存", Assert.Single(result.Model.Chapters).Title);
            Assert.Equal("新模块", Assert.Single(result.Graph!.Modules).Id);
            Assert.NotNull(result.Projection);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void CompilesInMemoryWStoryText()
    {
        var folder = Directory.CreateTempSubdirectory("weftline-test-");

        try
        {
            var chapterPath = Path.Combine(folder.FullName, "chapter.weft");
            var wstoryPath = Path.Combine(folder.FullName, "project.wstory");

            File.WriteAllText(chapterPath, """
            @title 第一章
            @module A
            @next -> END
            """);

            File.WriteAllText(wstoryPath, """
            @project 磁盘项目
                @chapters
                    @chapter 第一章 [磁盘描述。]
            """);

            var snapshot = new ProjectSnapshot(
                folder.FullName,
                1,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [wstoryPath] = """
                    @project 内存项目
                        @chapters
                            @chapter 第一章 [内存描述。]
                    """,
                });

            var result = ProjectCompiler.Compile(snapshot, cache: null);

            Assert.Equal("内存项目", result.Model.Project.Name);
            Assert.Equal("内存描述。", Assert.Single(result.Model.Chapters).Description);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void PartialCompilationKeepsGraphOnErrors()
    {
        var folder = Directory.CreateTempSubdirectory("weftline-test-");

        try
        {
            var path = Path.Combine(folder.FullName, "chapter.weft");
            var snapshot = new ProjectSnapshot(
                folder.FullName,
                2,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [path] = """
                    @title 第一章
                    @module A
                        [正文。]
                    @next -> END

                    无法识别的行
                    """,
                });

            var strict = ProjectCompiler.Compile(snapshot);
            Assert.True(strict.HasErrors);
            Assert.Null(strict.Graph);

            var partial = ProjectCompiler.Compile(
                snapshot,
                cache: null,
                options: new CompilationOptions(AllowPartial: true, BuildProjection: true));

            Assert.True(partial.HasErrors);
            Assert.NotEmpty(partial.Diagnostics);
            Assert.NotNull(partial.Graph);
            Assert.Single(partial.Graph!.Modules);
            Assert.NotNull(partial.Projection);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReusesParsedAstWhenTextIsUnchanged()
    {
        var folder = Directory.CreateTempSubdirectory("weftline-test-");

        try
        {
            var path = Path.Combine(folder.FullName, "chapter.weft");
            File.WriteAllText(path, """
            @title 第一章
            @module A
            @next -> END
            """);

            var snapshot = ProjectSnapshot.FromFolder(folder.FullName, 1);
            var cache = new CompilationCache();

            var first = ProjectCompiler.Compile(snapshot, cache);
            var second = ProjectCompiler.Compile(snapshot, cache);

            Assert.Same(
                Assert.Single(first.Project.WeftFiles).Ast,
                Assert.Single(second.Project.WeftFiles).Ast);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
