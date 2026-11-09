using Weftline.Editor.ViewModels;
using Weftline.Models;

namespace Weftline.Editor.Tests;

public sealed class EditorHostViewModelTests
{
    [Fact]
    public void SetDiagnosticsAppliesToOpenDocument()
    {
        var host = CreateHost();
        host.OpenFile("a.weft");
        var document = Assert.Single(host.Documents);
        var diagnostic = CreateDiagnostic("a.weft");

        host.SetDiagnostics([diagnostic]);

        Assert.Contains(diagnostic, document.Diagnostics);
    }

    [Fact]
    public void CachesDiagnosticsForLaterOpen()
    {
        var host = CreateHost();
        var diagnostic = CreateDiagnostic("a.weft");

        host.SetDiagnostics([diagnostic]);
        host.OpenFile("a.weft");

        var document = Assert.Single(host.Documents);
        Assert.Contains(diagnostic, document.Diagnostics);
    }

    [Fact]
    public void RaisesDocumentTextChanged()
    {
        var host = CreateHost();
        host.OpenFile("a.weft");
        var document = Assert.Single(host.Documents);
        EditorDocumentViewModel? changed = null;
        host.DocumentTextChanged += (_, item) => changed = item;

        document.Document.Insert(0, "x");

        Assert.Same(document, changed);
    }

    [Fact]
    public void CloseDocumentRemovesDocument()
    {
        var host = CreateHost();
        host.OpenFile("a.weft");
        var document = Assert.Single(host.Documents);

        host.CloseDocument(document);

        Assert.Empty(host.Documents);
        Assert.Null(host.SelectedDocument);
        Assert.False(host.HasDocument);
    }

    private static EditorHostViewModel CreateHost()
    {
        var loader = new FakeDocumentLoader();
        loader.Files["a.weft"] = """
        @title 第一章
        @module M
        @next -> END
        """;

        return new EditorHostViewModel(
            loader,
            new FakeIconProvider(),
            new FakeThemeSource());
    }

    private static WeftDiagnostic CreateDiagnostic(string filePath)
        => new(
            filePath,
            new TextSpan(0, 1),
            WeftDiagnosticSeverity.Error,
            "WFT1001",
            "语法错误");
}
