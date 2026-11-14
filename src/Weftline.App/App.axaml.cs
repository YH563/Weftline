using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Weftline.App.Controls;
using Weftline.App.Services;
using Weftline.App.ViewModels;
using Weftline.App.Views;

namespace Weftline.App;

public partial class App : Application
{
    public static string? GraphPreviewFolder { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (OperatingSystem.IsLinux())
        {
            RoundedClipFix.Enable();
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ThemeService.Initialize();

            if (GraphPreviewFolder is { } folder)
            {
                desktop.MainWindow = new GraphPreviewWindow(folder);
            }
            else
            {
                var viewModel = new MainWindowViewModel(new DialogService());
                desktop.MainWindow = new MainWindow
                {
                    DataContext = viewModel,
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
