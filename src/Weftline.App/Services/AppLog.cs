using System.Text;
using Avalonia.Threading;

namespace Weftline.App.Services;

/// <summary>
/// 极简本地日志与全局异常入口。日志写到用户数据目录，写入失败时静默降级到 stderr。
/// Minimal local logging and global exception hooks. Logs go to the user data
/// directory; if that fails, logging silently falls back to stderr.
/// </summary>
public static class AppLog
{
    private static readonly object Sync = new();
    private static bool _initialized;
    private static bool _uiInitialized;
    private static string? _logPath;

    public static string? LogPath
    {
        get
        {
            lock (Sync)
            {
                return _logPath;
            }
        }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Error(args.ExceptionObject as Exception, "AppDomain.UnhandledException");

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Error(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    public static void InitializeUi()
    {
        lock (Sync)
        {
            if (_uiInitialized)
            {
                return;
            }

            _uiInitialized = true;
        }

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            Error(args.Exception, "Dispatcher.UnhandledException");

            // 保住未保存内容，避免单个 UI 异常直接终止进程。
            // Keep unsaved work alive instead of terminating on a single UI exception.
            args.Handled = true;
        };
    }

    public static void Info(string message) => Write("INFO", message, exception: null);

    public static void Error(Exception? exception, string message)
        => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [")
            .Append(level)
            .Append("] ")
            .Append(message);

        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        var text = line.ToString();

        try
        {
            Console.Error.WriteLine(text);
        }
        catch (IOException)
        {
        }

        try
        {
            lock (Sync)
            {
                var path = EnsureLogFile();
                File.AppendAllText(path, text + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception writeError) when (
            writeError is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"写入日志失败：{writeError.Message}");
        }
    }

    private static string EnsureLogFile()
    {
        if (_logPath is not null)
        {
            return _logPath;
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        var directory = Path.Combine(root, "Weftline", "logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, $"app-{DateTime.Now:yyyyMMdd}.log");
        return _logPath;
    }
}
