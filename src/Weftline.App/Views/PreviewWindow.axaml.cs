using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Weftline.App.ViewModels;

namespace Weftline.App.Views;

/// <summary>
/// 应用内剧本预览窗口：逐句阅读、选项、图片和局部起点。
/// </summary>
public partial class PreviewWindow : Window
{
    private PreviewViewModel? _viewModel;

    public PreviewWindow()
    {
        InitializeComponent();
        Controls.OverlayLayerHost.Ensure(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.VisibleBlocks.CollectionChanged -= OnContentChanged;
            _viewModel.Choices.CollectionChanged -= OnContentChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PreviewViewModel;

        if (_viewModel is not null)
        {
            _viewModel.VisibleBlocks.CollectionChanged += OnContentChanged;
            _viewModel.Choices.CollectionChanged += OnContentChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Title = $"剧本预览 · {_viewModel.ProjectName}";
        }
        else
        {
            Title = "剧本预览";
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_viewModel is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                if (_viewModel.CanAdvance)
                {
                    _viewModel.Advance();
                    e.Handled = true;
                }
                break;
            case Key.D:
                _viewModel.IsDebugVisible = !_viewModel.IsDebugVisible;
                e.Handled = true;
                break;
            case Key.R:
                _viewModel.RestartCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }

        var choiceIndex = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => -1,
        };

        if (choiceIndex >= 0 && choiceIndex < _viewModel.Choices.Count)
        {
            _viewModel.Choose(choiceIndex);
            e.Handled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.VisibleBlocks.CollectionChanged -= OnContentChanged;
            _viewModel.Choices.CollectionChanged -= OnContentChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Dispose();
            _viewModel = null;
        }

        base.OnClosed(e);
    }

    private void OnReadingAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel is { CanAdvance: true })
        {
            _viewModel.Advance();
            e.Handled = true;
        }
    }

    private void OnContentChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScheduleScrollToEnd();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PreviewViewModel.HasChoices)
            or nameof(PreviewViewModel.IsEnded)
            or nameof(PreviewViewModel.HasError)
            or nameof(PreviewViewModel.CanAdvance))
        {
            ScheduleScrollToEnd();
        }
    }

    private void ScheduleScrollToEnd()
    {
        Dispatcher.UIThread.Post(
            () => ReaderScroll.ScrollToEnd(),
            DispatcherPriority.Background);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
