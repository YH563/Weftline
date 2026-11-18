using Weftline.Core;
using Weftline.Core.Runtime;
using Weftline.Native;

namespace Weftline.App.Services;

public sealed record ProjectAnalysisResult(
    NativeProjectResponse Native,
    GraphProjection? Projection,
    WeftProjectSymbols? Symbols,
    IReadOnlyDictionary<string, string> Documents,
    RuntimeProjection? Runtime);

/// <summary>
/// 项目级分析调度：防抖、单飞、取消与 latest-wins。
/// 文档快照和结果应用都回到 UI 线程，native 调用在后台串行执行。
/// </summary>
public sealed class ProjectAnalysisScheduler : IProjectAnalysisScheduler, IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(150);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<IReadOnlyDictionary<string, string>> _documentsProvider;
    private readonly Func<INativeProjectClient> _clientFactory;
    private readonly IUiDispatcher _dispatcher;
    private INativeProjectClient? _client;
    private CancellationTokenSource? _cts;
    private long _version;
    private bool _disposed;

    public ProjectAnalysisScheduler(
        Func<IReadOnlyDictionary<string, string>> documentsProvider,
        Func<INativeProjectClient>? clientFactory = null,
        IUiDispatcher? dispatcher = null)
    {
        _documentsProvider = documentsProvider;
        _clientFactory = clientFactory ?? (static () => new NativeWeftlineClient());
        _dispatcher = dispatcher ?? new AvaloniaUiDispatcher();
    }

    public event Action? Started;

    public event Action<ProjectAnalysisResult>? Completed;

    public event Action<Exception>? Failed;

    public void Schedule(string folder, bool reloadFromDisk, bool buildRuntime = false)
    {
        if (_disposed)
        {
            return;
        }

        _ = StartAsync(folder, reloadFromDisk, DebounceDelay, buildRuntime);
    }

    public Task RunNowAsync(string folder, bool reloadFromDisk, bool buildRuntime = false)
        => _disposed
            ? Task.CompletedTask
            : StartAsync(folder, reloadFromDisk, TimeSpan.Zero, buildRuntime);

    public void Cancel() => _cts?.Cancel();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();
        _cts = null;
        _ = DisposeClientAsync();
    }

    /// <summary>
    /// 取消当前分析并销毁 native 项目句柄，下一次编译会从磁盘全量重建。
    /// Cancels the current analysis and disposes the native project handle so
    /// the next compile rebuilds everything from disk.
    /// </summary>
    public async Task ResetAsync()
    {
        _cts?.Cancel();

        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            Interlocked.Increment(ref _version);
            _client?.Dispose();
            _client = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 用当前项目的编译缓存生成播放器载荷。
    /// 调用前应先完成一次项目分析，确保句柄里是最新文档。
    /// </summary>
    public async Task<RuntimeProjection> CompileRuntimeAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var client = _client
                ?? throw new InvalidOperationException("项目尚未完成首次编译。");

            return await Task.Run(
                () => client.CompileRuntime(),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task StartAsync(
        string folder,
        bool reloadFromDisk,
        TimeSpan delay,
        bool buildRuntime)
    {
        if (_disposed || string.IsNullOrWhiteSpace(folder))
        {
            return Task.CompletedTask;
        }

        _cts?.Cancel();

        var cts = new CancellationTokenSource();
        _cts = cts;
        var version = Interlocked.Increment(ref _version);
        return RunAsync(folder, reloadFromDisk, delay, version, cts, buildRuntime);
    }

    private async Task RunAsync(
        string folder,
        bool reloadFromDisk,
        TimeSpan delay,
        long version,
        CancellationTokenSource cts,
        bool buildRuntime)
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

                await _dispatcher.InvokeAsync(
                    () => Started?.Invoke());

                if (IsStale(version, cts))
                {
                    return;
                }

                var documents = await _dispatcher.InvokeAsync(
                    _documentsProvider);

                var result = await Task.Run(
                    () =>
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        _client ??= _clientFactory();

                        var projectDocuments = _client.LoadFolder(
                            folder, documents, reloadFromDisk);
                        cts.Token.ThrowIfCancellationRequested();

                        var native = _client.Compile(
                            allowPartial: true,
                            buildProjection: true,
                            buildRuntime: buildRuntime);

                        return new ProjectAnalysisResult(
                            native,
                            native.Projection?.ToGraphProjection(),
                            native.Symbols?.ToSymbols(),
                            projectDocuments,
                            native.Runtime?.ToRuntimeProjection());
                    },
                    cts.Token).ConfigureAwait(false);

                if (IsStale(version, cts))
                {
                    return;
                }

                await _dispatcher.InvokeAsync(
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
                await _dispatcher.InvokeAsync(
                    () => Failed?.Invoke(exception));
            }
        }
    }

    private async Task DisposeClientAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            _client?.Dispose();
            _client = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsStale(long version, CancellationTokenSource cts)
        => cts.IsCancellationRequested || version != Interlocked.Read(ref _version);
}
