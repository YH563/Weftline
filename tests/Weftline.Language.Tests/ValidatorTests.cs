using Weftline.Language.Diagnostics;
using Weftline.Language.Projects;
using Weftline.Language.Syntax;

namespace Weftline.Language.Tests;

public sealed class ValidatorTests
{
    [Fact]
    public void AcceptsValidGraph()
    {
        var result = Compile("""
        @title 第36章
        @module A
        @next -> END
        """);

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ReportsMissingTarget()
    {
        var result = Compile("""
        @title 第37章
        @module A
        @next -> Missing
        """);

        Assert.Contains(
            result.Diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.TargetNotFound);
    }

    [Fact]
    public void ReportsUnreachableModule()
    {
        var result = Compile("""
        @title 第38章
        @module A
        @next -> END

        @module B
        @next -> END
        """);

        Assert.Contains(
            result.Diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.UnreachableModule &&
                    item.Diagnostic.Message.Contains("B", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsPathCannotReachEnd()
    {
        var result = Compile("""
        @title 第39章
        @module A
        @next -> B

        @module B
        @next -> B
        """);

        Assert.Contains(
            result.Diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.PathCannotReachEnd);
    }

    [Fact]
    public void ReportsEmptyProject()
    {
        var loaded = new LoadedProject(
            "test",
            null,
            null,
            Array.Empty<ProjectFile>(),
            Array.Empty<ProjectDiagnostic>());
        var binding = ProjectBinder.Bind(Array.Empty<ProjectFile>(), null, null);

        var diagnostics = Validator.Validate(loaded, binding.Index);

        Assert.Contains(
            diagnostics,
            item => item.Diagnostic.Code == DiagnosticCode.EmptyProject);
    }

    private static (LoadedProject Project, ProjectIndex Index, IReadOnlyList<ProjectDiagnostic> Diagnostics) Compile(string source)
    {
        var parse = WeftlineCompiler.Parse(source, "test.weft");
        Assert.Empty(parse.Diagnostics);

        var file = new ProjectFile(
            "test.weft",
            Assert.IsType<WeftFileSyntax>(parse.Script));

        var loaded = new LoadedProject(
            "test",
            null,
            null,
            [file],
            Array.Empty<ProjectDiagnostic>());

        var binding = ProjectBinder.Bind([file], null, null);
        var diagnostics = Validator.Validate(loaded, binding.Index);

        return (loaded, binding.Index, diagnostics);
    }
}
