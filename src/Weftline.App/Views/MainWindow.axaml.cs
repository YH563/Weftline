using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Weftline.App.Features.Graph.Controls;
using Weftline.App.Features.Player.Views;
using Weftline.App.Services;
using Weftline.App.ViewModels;

namespace Weftline.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _subscribedViewModel;
    private PreviewWindow? _previewWindow;
    private bool _wasGraphVisible;
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();

        GraphView.ModuleActivated += OnModuleActivated;
        GraphView.ModuleEdgeActivated += OnModuleEdgeActivated;
        GraphView.ChapterEdgeActivated += OnChapterEdgeActivated;
        GraphView.ModuleEntryRequested += OnModuleEntryRequested;
        GraphView.ModuleHoverChanged += OnModuleHoverChanged;
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (e.Cancel ||
            _closeConfirmed ||
            DataContext is not MainWindowViewModel viewModel ||
            !viewModel.HasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;

        try
        {
            if (!await viewModel.ConfirmDiscardChangesAsync("关闭窗口").ConfigureAwait(true))
            {
                return;
            }

            _closeConfirmed = true;
            Close();
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, "关闭窗口前确认未保存修改失败");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Dispose();
        }

        base.OnClosed(e);
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

        _wasGraphVisible = false;
        UpdatePaneLayout();
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
        var becameVisible = graph && !_wasGraphVisible;
        _wasGraphVisible = graph;

        GraphPane.IsVisible = graph;
        PreviewPane.IsVisible = preview;
        PaneSplitter.IsVisible = graph && preview;

        if (graph && preview)
        {
            RightPaneGrid.RowDefinitions[0].Height = new GridLength(9, GridUnitType.Star);
            RightPaneGrid.RowDefinitions[1].Height = new GridLength(6);
            RightPaneGrid.RowDefinitions[2].Height = new GridLength(11, GridUnitType.Star);
        }
        else if (preview)
        {
            RightPaneGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            RightPaneGrid.RowDefinitions[1].Height = new GridLength(0);
            RightPaneGrid.RowDefinitions[2].Height = new GridLength(0);
        }
        else
        {
            RightPaneGrid.RowDefinitions[0].Height = new GridLength(0);
            RightPaneGrid.RowDefinitions[1].Height = new GridLength(0);
            RightPaneGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
        }

        if (becameVisible)
        {
            GraphView.ResetLayout();
        }
    }

    private void OnProjectOpened(object? sender, EventArgs e)
    {
        ClosePreview();
        GraphView.ResetLayout();
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

    private void OnLayoutSaveRequested(object? sender, EventArgs e)
    {
        if (_subscribedViewModel is { } viewModel)
        {
            viewModel.SaveLayout(GraphView.ExportLayout());
        }
    }

    private void OnModuleActivated(object? sender, ModuleActivatedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ActivateModule(e.Module);

    private void OnModuleEdgeActivated(object? sender, ModuleEdgeActivatedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ActivateModuleEdge(e.Edge);

    private void OnChapterEdgeActivated(object? sender, ChapterEdgeActivatedEventArgs e)
        => (DataContext as MainWindowViewModel)?.ActivateChapterEdge(e.Edge);

    private void OnModuleHoverChanged(object? sender, ModuleHoverChangedEventArgs e)
        => (DataContext as MainWindowViewModel)?.UpdateModuleHover(e.Module);

    private async void OnModuleEntryRequested(
        object? sender, ModuleEntryRequestedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.SetEntryAsync(e.Module);
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, $"设置入口失败：{e.Module.ModuleId}");
        }
    }

    private void OnDiagnosticDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel &&
            viewModel.SelectedDiagnostic is { } diagnostic)
        {
            viewModel.NavigateToDiagnostic(diagnostic);
        }
    }
}
