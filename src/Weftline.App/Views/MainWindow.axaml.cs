using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using SukiUI.Controls;
using Weftline.App.ViewModels;
using Weftline.Graph.Controls;

namespace Weftline.App.Views;

public partial class MainWindow : SukiWindow
{
    private MainWindowViewModel? _subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();

        GraphView.ModuleActivated += OnModuleActivated;
        GraphView.ModuleEdgeActivated += OnModuleEdgeActivated;
        GraphView.ChapterEdgeActivated += OnChapterEdgeActivated;
        GraphView.ModuleHoverChanged += OnModuleHoverChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ProjectOpened -= OnProjectOpened;
            _subscribedViewModel.LayoutSaveRequested -= OnLayoutSaveRequested;
        }

        _subscribedViewModel = DataContext as MainWindowViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ProjectOpened += OnProjectOpened;
            _subscribedViewModel.LayoutSaveRequested += OnLayoutSaveRequested;
        }

        ApplySavedLayout();
    }

    private void OnProjectOpened(object? sender, EventArgs e) => ApplySavedLayout();

    private void ApplySavedLayout()
    {
        GraphView.ResetLayout();

        if (_subscribedViewModel?.LoadLayout() is { } layout)
        {
            GraphView.ApplyLayout(layout);
        }
    }

    private void OnLayoutSaveRequested(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is { } viewModel)
        {
            viewModel.SaveLayout(GraphView.ExportLayout());
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (OperatingSystem.IsLinux())
        {
            RootCornerRadius = default;
        }
    }

    private void OnResetGraphLayout(object? sender, RoutedEventArgs e)
    {
        GraphView.ResetLayout();
        _subscribedViewModel?.ClearSavedLayout();
    }

    private void OnModuleActivated(object? sender, ModuleActivatedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ActivateModule(e.Module);

    private void OnModuleEdgeActivated(object? sender, ModuleEdgeActivatedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ActivateModuleEdge(e.Edge);

    private void OnChapterEdgeActivated(object? sender, ChapterEdgeActivatedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ActivateChapterEdge(e.Edge);

    private void OnModuleHoverChanged(object? sender, ModuleHoverChangedEventArgs e)
        => (DataContext as MainWindowViewModel)?.UpdateModuleHover(e.Module);

    private void OnDiagnosticDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.SelectedDiagnostic is { } diagnostic)
        {
            viewModel.NavigateToDiagnostic(diagnostic);
        }
    }
}
