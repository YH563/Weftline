using Avalonia.Threading;

namespace Weftline.App.Services;

/// <summary>
/// 监听当前项目顶层文件的磁盘变化，按 300ms 去抖后在 UI 线程通知。
/// Watches top-level project files and raises debounced notifications on the UI thread.
/// </summary>
public sealed class ProjectFolderWatcher : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);
    private static readonly string[] WatchedExtensions = [".weft", ".wstory"];

    private readonly object _sync = new();
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _timer;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public ProjectFolderWatcher(string folder)
    {
        _timer = new Timer(Flush, state: null, Timeout.Infinite, Timeout.Infinite);

        try
        {
            var fullFolder = Path.GetFullPath(folder);

            if (!Directory.Exists(fullFolder))
            {
                return;
            }

            _watcher = new FileSystemWatcher(fullFolder)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
                    | NotifyFilters.CreationTime,
            };

            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (
            exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            AppLog.Error(exception, $"无法监听项目目录：{folder}");
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    public event Action<IReadOnlyList<string>>? Changed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnChanged;
            _watcher.Created -= OnChanged;
            _watcher.Deleted -= OnChanged;
            _watcher.Renamed -= OnRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
            _watcher = null;
        }

        _timer.Dispose();
    }

    private void OnChanged(object? sender, FileSystemEventArgs e)
        => Enqueue(e.FullPath);

    private void OnRenamed(object? sender, RenamedEventArgs e)
    {
        Enqueue(e.OldFullPath);
        Enqueue(e.FullPath);
    }

    private void OnError(object? sender, ErrorEventArgs e)
        => AppLog.Error(e.GetException(), "项目目录监听错误");

    private void Enqueue(string path)
    {
        if (_disposed || !IsWatched(path))
        {
            return;
        }

        lock (_sync)
        {
            if (!_pending.Add(path))
            {
                return;
            }
        }

        try
        {
            _timer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void Flush(object? state)
    {
        string[] paths;

        lock (_sync)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            paths = [.. _pending];
            _pending.Clear();
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (!_disposed)
                {
                    Changed?.Invoke(paths);
                }
            },
            DispatcherPriority.Background);
    }

    private static bool IsWatched(string path)
    {
        if (Directory.Exists(path))
        {
            return true;
        }

        var extension = Path.GetExtension(path);
        return WatchedExtensions.Any(candidate =>
            string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }
}
