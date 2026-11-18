using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Weftline.App.Features.Graph.Controls;
using Weftline.App.Features.Player.ViewModels;
using Weftline.App.Features.Player.Views;
using Weftline.App.Services;
using Weftline.App.ViewModels;
using Weftline.App.Views;
using Weftline.Core.Runtime;
using Weftline.Native;

namespace Weftline.App.Tools;

internal static class ShotRunner
{
    public static void CaptureWindow(string outPath, string[] args)
    {
        InitializeHeadless();
        var dark = ApplyTheme(args);
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

        if (HasFlag(args, "--problems"))
        {
            viewModel.IsProblemsVisible = true;
        }

        if (HasFlag(args, "--preview-panel"))
        {
            viewModel.IsPreviewVisible = true;
            PumpUntilComplete(viewModel.EnsurePreviewAsync());
        }

        Dispatcher.UIThread.RunJobs();

        if (HasFlag(args, "--graph-select"))
        {
            var graph = FindGraph(window);
            var modules = viewModel.Graph.Projection?.Modules;
            var moduleId = modules is { Count: > 1 }
                ? modules[1].ModuleId
                : modules?.FirstOrDefault()?.ModuleId;

            if (graph is not null && moduleId is not null)
            {
                graph.SelectModule(moduleId);
                Dispatcher.UIThread.RunJobs();
            }
        }

        if (HasFlag(args, "--graph-select-chapter"))
        {
            var graph = FindGraph(window);
            var chapterId = viewModel.Graph.Projection?.Chapters
                .FirstOrDefault()
                ?.ChapterId;

            if (graph is not null && chapterId is not null)
            {
                graph.SelectChapter(chapterId);
                Dispatcher.UIThread.RunJobs();
            }
        }

        if (HasFlag(args, "--preview-choice"))
        {
            var choice = viewModel.Preview.Modules
                .SelectMany(module => module.Choices)
                .FirstOrDefault();
            choice?.OpenCommand?.Execute(null);
            Dispatcher.UIThread.RunJobs();
        }

        if (HasFlag(args, "--menu"))
        {
            var menu = window.GetVisualDescendants().OfType<Menu>().FirstOrDefault();
            if (menu is not null &&
                menu.Items.Count > 0 &&
                menu.Items[0] is MenuItem item)
            {
                item.IsSubMenuOpen = true;
            }
        }

        if (!TrySaveFrame(window, outPath, dark, "截图失败：窗口未完成渲染"))
        {
            Environment.Exit(1);
        }
    }

    public static void CapturePreview(string outPath, string[] args)
    {
        InitializeHeadless();
        var dark = ApplyTheme(args);
        ThemeService.Initialize();

        var project = GetOption(args, "--project")
            ?? Path.Combine(Directory.GetCurrentDirectory(), "samples");
        var start = GetOption(args, "--start");

        if (!TryCompileRuntime(project, out var runtime))
        {
            Environment.Exit(1);
            return;
        }

        var viewModel = new PreviewViewModel(runtime, project, start)
        {
            IsDebugVisible = HasFlag(args, "--debug"),
        };

        var window = new PreviewWindow
        {
            DataContext = viewModel,
        };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (int.TryParse(GetOption(args, "--advance"), out var steps))
        {
            for (var index = 0; index < steps; index++)
            {
                viewModel.Advance();
                Dispatcher.UIThread.RunJobs();
            }
        }

        if (HasFlag(args, "--combo"))
        {
            var combo = window.GetVisualDescendants().OfType<ComboBox>().FirstOrDefault();
            if (combo is not null)
            {
                combo.IsDropDownOpen = true;
            }
        }

        var captured = TrySaveFrame(window, outPath, dark, "截图失败：预览窗口未完成渲染");
        viewModel.Dispose();

        if (!captured)
        {
            Environment.Exit(1);
        }
    }

    private static void InitializeHeadless()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .LogToTrace()
            .SetupWithoutStarting();

    private static bool ApplyTheme(string[] args)
    {
        var dark = HasFlag(args, "--dark");

        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        return dark;
    }

    private static bool TryCompileRuntime(string project, out RuntimeProjection runtime)
    {
        try
        {
            using var client = new NativeWeftlineClient();
            client.LoadFolder(
                project,
                new Dictionary<string, string>(StringComparer.Ordinal),
                reloadFromDisk: true);
            runtime = client.CompileRuntime();
            return true;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"预览截图失败：{exception.Message}");
            runtime = null!;
            return false;
        }
    }

    private static bool TrySaveFrame(
        Window window,
        string outPath,
        bool dark,
        string failureMessage)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();

        if (frame is null)
        {
            Console.Error.WriteLine(failureMessage);
            return false;
        }

        frame.Save(outPath, PngBitmapEncoderOptions.Default);
        Console.WriteLine(
            $"已保存 {outPath} ({frame.PixelSize.Width}x{frame.PixelSize.Height}, " +
            $"{(dark ? "Dark" : "Light")})");
        return true;
    }

    private static StoryGraphView? FindGraph(Window window)
        => window.GetVisualDescendants().OfType<StoryGraphView>().FirstOrDefault();

    private static bool HasFlag(string[] args, string name)
        => Array.IndexOf(args, name) >= 0;

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
