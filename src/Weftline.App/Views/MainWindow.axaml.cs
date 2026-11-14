using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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

        EnablePopupOverlayLayer();

        GraphView.ModuleActivated += OnModuleActivated;
        GraphView.ModuleEdgeActivated += OnModuleEdgeActivated;
        GraphView.ChapterEdgeActivated += OnChapterEdgeActivated;
        GraphView.ModuleHoverChanged += OnModuleHoverChanged;
    }


    private void EnablePopupOverlayLayer()
    {
        var property = typeof(VisualLayerManager).GetProperty(
            "EnablePopupOverlayLayer",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        property?.SetValue(LayerManager, true);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ProjectOpened -= OnProjectOpened;
            _subscribedViewModel.LayoutSaveRequested -= OnLayoutSaveRequested;
            _subscribedViewModel.PreviewRequested -= OnPreviewRequested;
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = DataContext as MainWindowViewModel;

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.ProjectOpened += OnProjectOpened;
            _subscribedViewModel.LayoutSaveRequested += OnLayoutSaveRequested;
            _subscribedViewModel.PreviewRequested += OnPreviewRequested;
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdatePaneLayout();
        ApplySavedLayout();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsGraphVisible) ||
            e.PropertyName == nameof(MainWindowViewModel.IsPreviewVisible))
        {
            UpdatePaneLayout();
        }
    }

    private void UpdatePaneLayout()
    {
        var graph = _subscribedViewModel?.IsGraphVisible ?? true;
        var preview = _subscribedViewModel?.IsPreviewVisible ?? false;

        GraphPane.IsVisible = graph;
        PreviewPane.IsVisible = preview;
        PaneSplitter.IsVisible = graph && preview;

        if (graph && preview)
        {
            RightPaneGrid.RowDefinitions[1].Height = new GridLength(11, GridUnitType.Star);
            RightPaneGrid.RowDefinitions[2].Height = new GridLength(6);
            RightPaneGrid.RowDefinitions[3].Height = new GridLength(9, GridUnitType.Star);
        }
        else if (graph)
        {
            RightPaneGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            RightPaneGrid.RowDefinitions[2].Height = new GridLength(0);
            RightPaneGrid.RowDefinitions[3].Height = new GridLength(0);
        }
        else
        {
            RightPaneGrid.RowDefinitions[1].Height = new GridLength(0);
            RightPaneGrid.RowDefinitions[2].Height = new GridLength(0);
            RightPaneGrid.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);
        }
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

            // 透明窗口在部分 X11/Wayland 合成器上会把圆角外区域画成黑色。
            TransparencyLevelHint = [Avalonia.Controls.WindowTransparencyLevel.None];
            Background = new SolidColorBrush(Color.Parse("#F2F3F5"));
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
