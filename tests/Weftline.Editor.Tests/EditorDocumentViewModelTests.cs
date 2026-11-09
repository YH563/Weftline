using Weftline.Editor.ViewModels;
using Weftline.Models;

namespace Weftline.Editor.Tests;

public sealed class EditorDocumentViewModelTests
{
    [Fact]
    public void MergesSyntaxAndProjectDiagnosticsWithoutDuplicates()
    {
        var document = CreateDocument();
        var syntax = new WeftDiagnostic(
            "a.weft",
            new TextSpan(0, 1),
            WeftDiagnosticSeverity.Error,
            "WFT1001",
            "语法错误");
        var project = new WeftDiagnostic(
            "a.weft",
            new TextSpan(10, 2),
            WeftDiagnosticSeverity.Warning,
            "WFT4001",
            "项目警告");

        document.SetSyntaxDiagnostics([syntax]);
        document.SetProjectDiagnostics([syntax, project]);

        Assert.Equal(2, document.Diagnostics.Count);
        Assert.Contains(syntax, document.Diagnostics);
        Assert.Contains(project, document.Diagnostics);
    }

    [Fact]
    public void DoesNotRaiseDiagnosticsChangedForSameInput()
    {
        var document = CreateDocument();
        var diagnostics = new[]
        {
            new WeftDiagnostic(
                "a.weft",
                new TextSpan(0, 1),
                WeftDiagnosticSeverity.Error,
                "WFT1001",
                "语法错误"),
        };

        document.SetProjectDiagnostics(diagnostics);
        var raised = 0;
        document.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EditorDocumentViewModel.Diagnostics))
            {
                raised++;
            }
        };

        document.SetProjectDiagnostics(diagnostics.ToList());
        document.SetSyntaxDiagnostics([]);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void OpenFileAndUpdateTextRefreshTitle()
    {
        var loader = new FakeDocumentLoader();
        loader.Files["a.weft"] = """
        @title 第一章
        @module M
        @next -> END
        """;
        var document = new EditorDocumentViewModel(loader, new FakeIconProvider());

        document.OpenFile("a.weft");

        Assert.Equal("第一章", document.Title);
        Assert.False(document.IsModified);
        Assert.True(document.Version > 0);

        document.UpdateText("""
        @title 第二章
        @module M
        @next -> END
        """, true);

        Assert.Equal("第二章", document.Title);
        Assert.True(document.IsModified);
    }

    [Fact]
    public void GetOffsetClampsLineAndColumn()
    {
        var loader = new FakeDocumentLoader();
        loader.Files["a.weft"] = "abc\ndef";
        var document = new EditorDocumentViewModel(loader, new FakeIconProvider());

        document.OpenFile("a.weft");

        Assert.Equal(0, document.GetOffset(1, 1));
        Assert.Equal(5, document.GetOffset(2, 2));
        Assert.Equal(7, document.GetOffset(99, 99));
    }

    private static EditorDocumentViewModel CreateDocument()
        => new(new FakeDocumentLoader(), new FakeIconProvider());
}
