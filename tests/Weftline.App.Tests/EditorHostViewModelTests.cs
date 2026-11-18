using Weftline.App.Features.Editor.ViewModels;
using Weftline.Core;

namespace Weftline.App.Tests;

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

    [Fact]
    public void MissingFileDoesNotOpenAndRaisesFailure()
    {
        var host = CreateHost();
        EditorOpenFailedEventArgs? failure = null;
        host.OpenFailed += (_, args) => failure = args;

        var opened = host.OpenFile("missing.weft");

        Assert.False(opened);
        Assert.Empty(host.Documents);
        Assert.NotNull(failure);
        Assert.Equal("missing.weft", failure.FilePath);
    }

    [Fact]
    public async Task ModifiedTabCloseCanBeCancelled()
    {
        var host = CreateHost();
        host.OpenFile("a.weft");
        var document = Assert.Single(host.Documents);
        document.UpdateText("changed", markModified: true);
        host.CloseConfirmation = _ => Task.FromResult(false);

        var closed = await host.CloseDocumentAsync(document);

        Assert.False(closed);
        Assert.Contains(document, host.Documents);

        host.CloseConfirmation = _ => Task.FromResult(true);

        Assert.True(await host.CloseDocumentAsync(document));
        Assert.Empty(host.Documents);
    }

    [Fact]
    public void ReloadsUnmodifiedDocumentsAndReportsConflicts()
    {
        var loader = new FakeDocumentLoader();
        loader.Files["a.weft"] = "@title 旧";
        loader.Files["b.weft"] = "@title 未修改";
        var host = new EditorHostViewModel(
            loader,
            new FakeIconProvider(),
            new FakeThemeSource());

        host.OpenFile("a.weft");
        host.OpenFile("b.weft");
        var first = host.Documents[0];
        var second = host.Documents[1];
        first.UpdateText("@title 未保存", markModified: true);

        loader.Files["a.weft"] = "@title 磁盘";
        loader.Files["b.weft"] = "@title 新磁盘";

        var result = host.ReloadDocumentsFromDisk(["a.weft", "b.weft"]);

        Assert.Equal(1, result.Reloaded);
        Assert.Equal(1, result.Conflicts);
        Assert.Equal("@title 未保存", first.GetTextSnapshot());
        Assert.Equal("@title 新磁盘", second.GetTextSnapshot());
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
