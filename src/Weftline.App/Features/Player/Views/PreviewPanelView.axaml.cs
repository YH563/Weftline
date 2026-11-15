using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Weftline.App.Features.Player.ViewModels;

namespace Weftline.App.Features.Player.Views;

public partial class PreviewPanelView : UserControl
{
    private PreviewPanelViewModel? _viewModel;

    public PreviewPanelView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.ModulePreviewRequested -= OnModulePreviewRequested;
        }

        _viewModel = DataContext as PreviewPanelViewModel;

        if (_viewModel is not null)
        {
            _viewModel.ModulePreviewRequested += OnModulePreviewRequested;
        }
    }

    private void SetCurrentModule(PreviewModuleViewModel? module)
    {
        if (_viewModel is not null)
        {
            foreach (var item in _viewModel.Modules)
            {
                item.IsCurrent = ReferenceEquals(item, module);
            }
        }
    }

    private void OnModuleTitleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: PreviewModuleViewModel module })
        {
            SetCurrentModule(module);
        }
    }

    private void OnModulePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: PreviewModuleViewModel module })
        {
            SetCurrentModule(module);
        }
    }

    private void OnModulePreviewRequested(string moduleId)
    {
        if (_viewModel is null)
        {
            return;
        }

        var module = _viewModel.Modules.FirstOrDefault(module =>
            string.Equals(module.ModuleId, moduleId, StringComparison.Ordinal));

        if (module is null)
        {
            return;
        }

        var container = ModuleList.ContainerFromItem(module) as Control
            ?? ModuleList.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .FirstOrDefault(presenter => ReferenceEquals(presenter.DataContext, module));

        if (container is null)
        {
            return;
        }

        var top = container.TranslatePoint(new Point(0, 0), PreviewScroll)?.Y;
        if (top is { } currentTop)
        {
            var targetOffset = PreviewScroll.Offset.Y + currentTop;
            PreviewScroll.Offset = new Vector(
                PreviewScroll.Offset.X,
                Math.Max(0, targetOffset));
        }
        else
        {
            container.BringIntoView();
        }

        SetCurrentModule(module);
    }
}
