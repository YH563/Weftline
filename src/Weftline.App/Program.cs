using Avalonia;
using Weftline.App.Services;
using Weftline.App.Tools;

namespace Weftline.App;

sealed class Program
{
    // 初始化代码。在 AppMain 调用之前，不要使用 Avalonia、第三方 API
    // 或依赖 SynchronizationContext 的代码：此时尚未初始化，可能会出错。
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppLog.Initialize();

        var previewShotIndex = Array.IndexOf(args, "--preview-shot");
        if (previewShotIndex >= 0)
        {
            var outPath = GetOutputPath(args, previewShotIndex, "weftline-player.png");
            ShotRunner.CapturePreview(outPath, args);
            return;
        }

        var shotIndex = Array.IndexOf(args, "--shot");
        if (shotIndex >= 0)
        {
            var outPath = GetOutputPath(args, shotIndex, "weftline-preview.png");
            ShotRunner.CaptureWindow(outPath, args);
            return;
        }

        if (args.Length >= 2 &&
            string.Equals(args[0], "--graph-preview", StringComparison.Ordinal))
        {
            App.GraphPreviewFolder = args[1];
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia 配置，不要删除；可视化设计器也会使用它。
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

        if (OperatingSystem.IsLinux())
        {
            builder = builder.With(new X11PlatformOptions
            {
                OverlayPopups = true,
            });
        }

        return builder;
    }

    private static string GetOutputPath(string[] args, int optionIndex, string fallback)
        => optionIndex + 1 < args.Length && !args[optionIndex + 1].StartsWith("--")
            ? args[optionIndex + 1]
            : fallback;
}
