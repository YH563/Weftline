using Avalonia;
using Weftline.App.Features.Editor.ViewModels;
using Weftline.App.Features.Graph.Layout;
using Weftline.App.Services;
using Weftline.App.ViewModels;
using Weftline.Core;
using Weftline.Core.Language;
using Weftline.Native;

namespace Weftline.App.Tests;

public sealed class MainWindowViewModelTests
{
    private const string ScriptText = """
    @title A
    @module M
        [正文]
    @next -> END
    """;

    [Fact]
    public async Task CompletedAnalysisUpdatesDiagnosticsGraphAndStatus()
    {
        var context = await CreateContextAsync();
        var native = CreateNativeResult(context.FilePath);
        var projection = native.Projection!.ToGraphProjection();

        context.Scheduler.Complete(new ProjectAnalysisResult(
            native,
            projection,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [context.FilePath] = ScriptText,
            },
            null));

        Assert.Single(context.ViewModel.Diagnostics);
        Assert.NotNull(context.ViewModel.Graph.Projection);
        Assert.Single(context.ViewModel.Graph.Projection!.Modules);
        Assert.Equal("1 错误 · 0 警告", context.ViewModel.Status.DiagnosticsText);
        Assert.Contains("分析完成", context.ViewModel.Status.StatusText);
    }

    [Fact]
    public async Task FailedAnalysisKeepsLastUsableGraph()
    {
        var context = await CreateContextAsync();
        var native = CreateNativeResult(context.FilePath);
        var projection = native.Projection!.ToGraphProjection();

        context.Scheduler.Complete(new ProjectAnalysisResult(
            native,
            projection,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            null));

        context.Scheduler.Fail(new InvalidOperationException("boom"));

        Assert.Same(projection, context.ViewModel.Graph.Projection);
        Assert.Empty(context.ViewModel.Diagnostics);
        Assert.Equal("分析失败：boom", context.ViewModel.Status.StatusText);
    }

    [Fact]
    public async Task SetEntryEditsAndSavesWStory()
    {
        var context = await CreateContextAsync();
        var wstoryPath = Path.Combine(context.Folder, "project.wstory");
        const string original = "@project 雨夜\n";

        File.WriteAllText(wstoryPath, original);
        context.Loader.Files[wstoryPath] = original;

        var module = new GraphModuleNode(
            "M",
            "第一章",
            [],
            0,
            GraphModuleFlags.None,
            new GraphModuleSummary(null, 0, 0),
            new SourceLocation(context.FilePath, new TextSpan(0, 0)));

        await context.ViewModel.SetEntryAsync(module);

        var save = Assert.Single(
            context.Saver.Saves,
            entry => entry.Path == wstoryPath);
        Assert.Contains("@entry M", save.Text);
    }

    [Fact]
    public async Task GenerateProjectFileMarksModifiedWithoutSaving()
    {
        var context = await CreateContextAsync();
        var wstoryPath = Path.Combine(context.Folder, "project.wstory");
        const string original = "@project 雨夜\n";

        File.WriteAllText(wstoryPath, original);
        context.Loader.Files[wstoryPath] = original;
        context.Scheduler.GeneratedWStory =
            "@project 雨夜\n    @entry 序\n    @chars\n        @char 萧影 []\n";

        await context.ViewModel.GenerateProjectFileCommand.ExecuteAsync(null);

        var document = context.ViewModel.Editor.SelectedDocument;
        Assert.NotNull(document);
        Assert.True(document!.IsModified);
        Assert.Contains("@entry 序", document.GetTextSnapshot());
        Assert.DoesNotContain(
            context.Saver.Saves,
            entry => entry.Path == wstoryPath);
    }

    [Fact]
    public async Task SaveLayoutDelegatesToLayoutStore()
    {
        var context = await CreateContextAsync();
        var snapshot = new GraphLayoutSnapshot(
            new Dictionary<string, Rect>(StringComparer.Ordinal)
            {
                ["M"] = new Rect(0, 0, 10, 10),
            },
            new Dictionary<string, Rect>(StringComparer.Ordinal),
            null,
            1);

        context.ViewModel.SaveLayout(snapshot);

        Assert.Equal(context.Folder, context.Layouts.SavedFolder);
        Assert.NotNull(context.Layouts.SavedSnapshot);
    }

    [Fact]
    public async Task LoadLayoutDelegatesToLayoutStore()
    {
        var context = await CreateContextAsync();
        var snapshot = new GraphLayoutSnapshot(
            new Dictionary<string, Rect>(StringComparer.Ordinal),
            new Dictionary<string, Rect>(StringComparer.Ordinal),
            null,
            1);
        context.Layouts.LoadResult = snapshot;

        Assert.Same(snapshot, context.ViewModel.LoadLayout());
    }

    [Fact]
    public async Task SaveCommandWritesThroughDocumentSaver()
    {
        var context = await CreateContextAsync();
        var document = context.ViewModel.Editor.SelectedDocument;
        Assert.NotNull(document);

        document!.Document.Insert(0, "\n");

        await context.ViewModel.SaveCommand.ExecuteAsync(null);

        var save = Assert.Single(context.Saver.Saves);
        Assert.Equal(document.FilePath, save.Path);
        Assert.Contains("@title A", save.Text);
        Assert.False(document.IsModified);
    }

    private static NativeProjectResponse CreateNativeResult(string filePath)
        => new()
        {
            Diagnostics =
            [
                new NativeDiagnostic
                {
                    Code = "WFT1001",
                    Severity = "error",
                    Message = "环边",
                    FilePath = filePath,
                    Start = 0,
                    Length = 5,
                },
            ],
            Graph = new NativeStoryGraph(),
            Projection = new NativeProjection
            {
                EntryModuleId = "M",
                Chapters =
                [
                    new NativeGraphChapter
                    {
                        ChapterId = "C1",
                        Title = "第一章",
                        ModuleIds = ["M"],
                        Flags = ["entry"],
                    },
                ],
                Modules =
                [
                    new NativeGraphModuleNode
                    {
                        ModuleId = "M",
                        ChapterId = "C1",
                        Source = new NativeSourceLocation
                        {
                            FilePath = filePath,
                            Span = new NativeTextSpan { Start = 10, Length = 1 },
                        },
                    },
                ],
            },
        };

    private static async Task<TestContext> CreateContextAsync()
    {
        var folder = Path.Combine(
            AppContext.BaseDirectory,
            Path.Combine("test-projects", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);

        var filePath = Path.Combine(folder, "a.weft");
        File.WriteAllText(filePath, ScriptText);

        var loader = new FakeDocumentLoader();
        loader.Files[filePath] = ScriptText;

        var dialogs = new FakeDialogService();
        var scheduler = new FakeProjectAnalysisScheduler();
        var layouts = new FakeLayoutStore();
        var saver = new FakeDocumentSaver();
        var language = new EditorLanguageService(new FakeSyntaxParser());
        var editor = new EditorHostViewModel(
            loader,
            new FakeIconProvider(),
            new FakeThemeSource(),
            language);

        var services = new MainWindowServices(
            dialogs,
            scheduler,
            layouts,
            saver,
            language,
            new ProjectExplorerViewModel(folder, dialogs),
            editor,
            new DocumentSnapshotProvider(editor));

        var viewModel = new MainWindowViewModel(services);
        await viewModel.LoadProjectForPreviewAsync(folder, "a.weft");

        return new TestContext(viewModel, scheduler, layouts, saver, loader, folder, filePath);
    }

    private sealed record TestContext(
        MainWindowViewModel ViewModel,
        FakeProjectAnalysisScheduler Scheduler,
        FakeLayoutStore Layouts,
        FakeDocumentSaver Saver,
        FakeDocumentLoader Loader,
        string Folder,
        string FilePath);
}
