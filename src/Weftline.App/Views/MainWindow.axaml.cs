using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SukiUI.Controls;
using Weftline.App.ViewModels;

namespace Weftline.App.Views;

public partial class MainWindow : SukiWindow
{
    private MainWindowViewModel? _subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ProjectOpened -= OnProjectOpened;
        }

        _subscribedViewModel = DataContext as MainWindowViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ProjectOpened += OnProjectOpened;
        }
    }

    private void OnProjectOpened(object? sender, EventArgs e)
        => GraphView.ResetLayout();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (OperatingSystem.IsLinux())
        {
            RootCornerRadius = default;
        }
    }

    private void OnResetGraphLayout(object? sender, RoutedEventArgs e)
        => GraphView.ResetLayout();
}
