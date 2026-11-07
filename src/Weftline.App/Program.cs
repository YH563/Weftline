using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Weftline.App.Services;
using Weftline.App.ViewModels;
using Weftline.App.Views;

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
        var shotIndex = Array.IndexOf(args, "--shot");
        if (shotIndex >= 0)
        {
            var outPath = shotIndex + 1 < args.Length && !args[shotIndex + 1].StartsWith("--")
                ? args[shotIndex + 1]
                : "weftline-preview.png";
            Capture(outPath, args);
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
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    private static void Capture(string outPath, string[] args)
    {
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .LogToTrace()
            .SetupWithoutStarting();

        var dark = Array.IndexOf(args, "--dark") >= 0;
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        ThemeService.Initialize();

        var viewModel = new MainWindowViewModel(new DialogService());
        var window = new MainWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var project = GetOption(args, "--project");
        if (project is not null)
        {
            var file = GetOption(args, "--file");
            PumpUntilComplete(viewModel.LoadProjectForPreviewAsync(project, file));
        }
        else if (GetOption(args, "--file") is { } standaloneFile)
        {
            viewModel.Editor.OpenFile(standaloneFile);
        }

        if (Array.IndexOf(args, "--problems") >= 0)
        {
            viewModel.IsProblemsVisible = true;
        }

        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            Console.Error.WriteLine("截图失败：窗口未完成渲染");
            Environment.Exit(1);
            return;
        }

        frame.Save(outPath, PngBitmapEncoderOptions.Default);
        Console.WriteLine(
            $"已保存 {outPath} ({frame.PixelSize.Width}x{frame.PixelSize.Height}, " +
            $"{(dark ? "Dark" : "Light")})");
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--")
            ? args[index + 1]
            : null;
    }

    private static void PumpUntilComplete(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }

        task.GetAwaiter().GetResult();
    }
}
