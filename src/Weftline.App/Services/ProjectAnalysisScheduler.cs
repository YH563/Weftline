using Avalonia.Threading;
using Weftline.Models;
using Weftline.Native;

namespace Weftline.App.Services;

public sealed record ProjectAnalysisResult(
    NativeProjectResponse Native,
    GraphProjection? Projection,
    WeftProjectSymbols? Symbols,
    IReadOnlyDictionary<string, string> Documents);

/// <summary>
/// 项目级分析调度：防抖、单飞、取消与 latest-wins。
/// 文档快照和结果应用都回到 UI 线程，native 调用在后台串行执行。
/// </summary>
public sealed class ProjectAnalysisScheduler
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(150);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<IReadOnlyDictionary<string, string>> _documentsProvider;
    private NativeWeftlineClient? _client;
    private CancellationTokenSource? _cts;
    private long _version;

    public ProjectAnalysisScheduler(
        Func<IReadOnlyDictionary<string, string>> documentsProvider)
    {
        _documentsProvider = documentsProvider;
    }

    public event Action? Started;

    public event Action<ProjectAnalysisResult>? Completed;

    public event Action<Exception>? Failed;

    public void Schedule(string folder, bool reloadFromDisk)
        => _ = StartAsync(folder, reloadFromDisk, DebounceDelay);

    public Task RunNowAsync(string folder, bool reloadFromDisk)
        => StartAsync(folder, reloadFromDisk, TimeSpan.Zero);

    public void Cancel() => _cts?.Cancel();

    private Task StartAsync(string folder, bool reloadFromDisk, TimeSpan delay)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return Task.CompletedTask;
        }

        _cts?.Cancel();

        var cts = new CancellationTokenSource();
        _cts = cts;
        var version = Interlocked.Increment(ref _version);
        return RunAsync(folder, reloadFromDisk, delay, version, cts);
    }

    private async Task RunAsync(
        string folder,
        bool reloadFromDisk,
        TimeSpan delay,
        long version,
        CancellationTokenSource cts)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cts.Token).ConfigureAwait(false);
            }

            await _gate.WaitAsync(cts.Token).ConfigureAwait(false);

            try
            {
                if (IsStale(version, cts))
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(
                    () => Started?.Invoke());

                if (IsStale(version, cts))
                {
                    return;
                }

                var documents = await Dispatcher.UIThread.InvokeAsync(
                    _documentsProvider);

                var result = await Task.Run(
                    () =>
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        _client ??= new NativeWeftlineClient();

                        var projectDocuments = _client.LoadFolder(
                            folder, documents, reloadFromDisk);
                        cts.Token.ThrowIfCancellationRequested();

                        var native = _client.Compile(
                            allowPartial: true, buildProjection: true);

                        return new ProjectAnalysisResult(
                            native,
                            native.Projection?.ToGraphProjection(),
                            native.Symbols?.ToSymbols(),
                            projectDocuments);
                    },
                    cts.Token).ConfigureAwait(false);

                if (IsStale(version, cts))
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(
                    () => Completed?.Invoke(result));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!IsStale(version, cts))
            {
                await Dispatcher.UIThread.InvokeAsync(
                    () => Failed?.Invoke(exception));
            }
        }
    }

    private bool IsStale(long version, CancellationTokenSource cts)
        => cts.IsCancellationRequested || version != Interlocked.Read(ref _version);
}
