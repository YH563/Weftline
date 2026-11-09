using Avalonia.Headless;
using Weftline.Editor.Services;
using Weftline.Editor.ViewModels;
using Weftline.Models;

namespace Weftline.Editor.Tests;

public sealed class SyntaxAnalysisSchedulerTests
{
    [Fact]
    public async Task AppliesSyntaxDiagnosticsForLatestVersion()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

        await session.Dispatch(async () =>
        {
            var loader = new FakeDocumentLoader();
            loader.Files["a.weft"] = """
            @title 第一章
            @module M
            @next -> END
            """;
            var document = new EditorDocumentViewModel(
                loader,
                new FakeIconProvider());
            document.OpenFile("a.weft");

            var diagnostic = new WeftDiagnostic(
                "a.weft",
                new TextSpan(0, 1),
                WeftDiagnosticSeverity.Error,
                "WFT1001",
                "语法错误");
            var languageService = new FakeLanguageService
            {
                SyntaxHandler = (_, _, _) => [diagnostic],
            };
            var scheduler = new SyntaxAnalysisScheduler(languageService);
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            document.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(EditorDocumentViewModel.Diagnostics))
                {
                    completion.TrySetResult(true);
                }
            };

            scheduler.Schedule(document);

            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Contains(diagnostic, document.Diagnostics);
            Assert.Equal("a.weft", languageService.LastFilePath);
            Assert.Equal(document.Version, languageService.LastVersion);

            scheduler.Cancel(document);
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task DeduplicatesSyntaxAndProjectDiagnostics()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

        await session.Dispatch(async () =>
        {
            var loader = new FakeDocumentLoader();
            loader.Files["a.weft"] = """
            @title 第一章
            @module M
            @next -> END
            """;
            var document = new EditorDocumentViewModel(
                loader,
                new FakeIconProvider());
            document.OpenFile("a.weft");

            var syntaxDiagnostic = new WeftDiagnostic(
                "a.weft",
                new TextSpan(0, 1),
                WeftDiagnosticSeverity.Error,
                "WFT1001",
                "语法错误");
            var projectDiagnostic = new WeftDiagnostic(
                "a.weft",
                new TextSpan(10, 2),
                WeftDiagnosticSeverity.Warning,
                "WFT4001",
                "项目警告");
            document.SetProjectDiagnostics([projectDiagnostic]);

            var languageService = new FakeLanguageService
            {
                SyntaxHandler = (_, _, _) => [syntaxDiagnostic, projectDiagnostic],
            };
            var scheduler = new SyntaxAnalysisScheduler(languageService);
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            document.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(EditorDocumentViewModel.Diagnostics))
                {
                    completion.TrySetResult(true);
                }
            };

            scheduler.Schedule(document);

            await completion.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(2, document.Diagnostics.Count);
            Assert.Contains(syntaxDiagnostic, document.Diagnostics);
            Assert.Single(
                document.Diagnostics,
                item => item.Code == projectDiagnostic.Code);

            scheduler.Cancel(document);
            return true;
        }, CancellationToken.None);
    }
}
