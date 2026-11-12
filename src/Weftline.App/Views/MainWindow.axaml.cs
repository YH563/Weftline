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
    private PreviewWindow? _previewWindow;

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
            _subscribedViewModel.PreviewRequested -= OnPreviewRequested;
        }

        _subscribedViewModel = DataContext as MainWindowViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ProjectOpened += OnProjectOpened;
            _subscribedViewModel.LayoutSaveRequested += OnLayoutSaveRequested;
            _subscribedViewModel.PreviewRequested += OnPreviewRequested;
        }

        ApplySavedLayout();
    }

    private void OnProjectOpened(object? sender, EventArgs e)
    {
        ClosePreview();
        ApplySavedLayout();
    }

    private void OnPreviewRequested(object? sender, PreviewRequestedEventArgs e)
    {
        ClosePreview();

        var window = new PreviewWindow
        {
            DataContext = e.ViewModel,
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_previewWindow, window))
            {
                _previewWindow = null;
            }
        };

        _previewWindow = window;
        window.Show(this);
    }

    private void ClosePreview()
    {
        _previewWindow?.Close();
        _previewWindow = null;
    }

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
