using Weftline.App.Services;

namespace Weftline.App.Tests;

public sealed class ProjectAnalysisSchedulerTests
{
    [Fact]
    public async Task ScheduleCoalescesBurstIntoOneCompile()
    {
        var client = new FakeNativeProjectClient();
        var scheduler = new ProjectAnalysisScheduler(
            () => new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["a.weft"] = "@title A",
            },
            () => client,
            new ImmediateUiDispatcher());

        var completed = new TaskCompletionSource<ProjectAnalysisResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.Completed += result => completed.TrySetResult(result);

        scheduler.Schedule("C:\\project", reloadFromDisk: false);
        scheduler.Schedule("C:\\project", reloadFromDisk: false);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, client.CompileCount);
    }

    [Fact]
    public async Task ResetDisposesNativeClient()
    {
        var client = new FakeNativeProjectClient();
        var scheduler = new ProjectAnalysisScheduler(
            () => new Dictionary<string, string>(StringComparer.Ordinal),
            () => client,
            new ImmediateUiDispatcher());

        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.Completed += _ => completed.TrySetResult(true);

        await scheduler.RunNowAsync("C:\\project", reloadFromDisk: true);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, client.CompileCount);

        await scheduler.ResetAsync();

        Assert.Equal(1, client.DisposeCount);
    }

    [Fact]
    public async Task FailedAnalysisRaisesFailedEvent()
    {
        var client = new ThrowingNativeProjectClient();
        var scheduler = new ProjectAnalysisScheduler(
            () => new Dictionary<string, string>(StringComparer.Ordinal),
            () => client,
            new ImmediateUiDispatcher());

        var failed = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        scheduler.Failed += exception => failed.TrySetResult(exception);

        await scheduler.RunNowAsync("C:\\project", reloadFromDisk: true);

        var error = await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsType<InvalidOperationException>(error);
    }

    private sealed class ThrowingNativeProjectClient : Weftline.Native.INativeProjectClient
    {
        public string Version => "fake";

        public IReadOnlyDictionary<string, string> LoadFolder(
            string folder,
            IReadOnlyDictionary<string, string> openDocuments,
            bool reloadFromDisk = false)
            => openDocuments;

        public Weftline.Native.NativeProjectResponse Compile(
            bool allowPartial,
            bool buildProjection,
            bool buildRuntime = false)
            => throw new InvalidOperationException("native compile failed");

        public Weftline.Core.Runtime.RuntimeProjection CompileRuntime()
            => throw new InvalidOperationException("native compile failed");

        public void Dispose()
        {
        }
    }
}
