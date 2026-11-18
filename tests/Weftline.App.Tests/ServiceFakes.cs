using Weftline.App.Features.Graph.Layout;
using Weftline.App.Services;
using Weftline.Core;
using Weftline.Core.Language;
using Weftline.Core.Runtime;
using Weftline.Native;

namespace Weftline.App.Tests;

internal sealed class FakeDialogService : IDialogService
{
    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<string?> PromptAsync(
        string title,
        string message,
        string initialValue = "",
        CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class FakeNativeProjectClient : INativeProjectClient
{
    public string Version => "fake";

    public int CompileCount { get; private set; }

    public int DisposeCount { get; private set; }

    public NativeCompileFlags LastFlags { get; private set; }

    public NativeProjectResponse Response { get; set; } = new();

    public IReadOnlyDictionary<string, string> LoadFolder(
        string folder,
        IReadOnlyDictionary<string, string> openDocuments,
        bool reloadFromDisk = false)
        => openDocuments;

    public NativeProjectResponse Compile(NativeCompileFlags flags)
    {
        CompileCount++;
        LastFlags = flags;
        return Response;
    }

    public RuntimeProjection CompileRuntime() => throw new NotSupportedException();

    public void Dispose() => DisposeCount++;
}

internal sealed class FakeProjectAnalysisScheduler : IProjectAnalysisScheduler
{
    public event Action? Started;

    public event Action<ProjectAnalysisResult>? Completed;

    public event Action<Exception>? Failed;

    public int RunCount { get; private set; }

    public int ResetCount { get; private set; }

    public string? LastFolder { get; private set; }

    public void Schedule(string folder, bool reloadFromDisk, bool buildRuntime = false)
    {
        RunCount++;
        LastFolder = folder;
    }

    public Task RunNowAsync(string folder, bool reloadFromDisk, bool buildRuntime = false)
    {
        RunCount++;
        LastFolder = folder;
        return Task.CompletedTask;
    }

    public Task<RuntimeProjection> CompileRuntimeAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task ResetAsync()
    {
        ResetCount++;
        return Task.CompletedTask;
    }

    public void Cancel()
    {
    }

    public void RaiseStarted() => Started?.Invoke();

    public void Complete(ProjectAnalysisResult result) => Completed?.Invoke(result);

    public void Fail(Exception exception) => Failed?.Invoke(exception);
}

internal sealed class FakeLayoutStore : IProjectLayoutStore
{
    public string? SavedFolder { get; private set; }

    public GraphLayoutSnapshot? SavedSnapshot { get; private set; }

    public string? ClearedFolder { get; private set; }

    public GraphLayoutSnapshot? LoadResult { get; set; }

    public GraphLayoutSnapshot? Load(string folder) => LoadResult;

    public void Save(string folder, GraphLayoutSnapshot snapshot)
    {
        SavedFolder = folder;
        SavedSnapshot = snapshot;
    }

    public void Clear(string folder) => ClearedFolder = folder;
}

internal sealed class FakeDocumentSaver : IDocumentSaver
{
    public List<(string Path, string Text)> Saves { get; } = [];

    public Task SaveAsync(string path, string text, CancellationToken cancellationToken = default)
    {
        Saves.Add((path, text));
        return Task.CompletedTask;
    }
}

internal sealed class FakeSyntaxParser : IWeftSyntaxParser
{
    public IReadOnlyList<WeftDiagnostic> Result { get; set; } = [];

    public ValueTask<IReadOnlyList<WeftDiagnostic>> ParseAsync(
        string filePath,
        string text,
        long version,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(Result);
}

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
        => Task.FromResult(function());
}
