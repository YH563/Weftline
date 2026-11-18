using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppLog.Initialize();
        AppLog.InitializeUi();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ThemeService.Initialize();
            AppLog.Info("应用启动");

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
