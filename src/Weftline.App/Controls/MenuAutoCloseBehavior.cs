using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Weftline.App.Controls;

/// <summary>
/// 顶层菜单在鼠标离开菜单栏和弹层后自动收起。
/// Avalonia 默认只在点击外部或切换顶层项时关闭，这里补上指针离开的行为。
/// </summary>
public sealed class MenuAutoCloseBehavior : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(100);
    private const int CloseAfterMisses = 3;

    private readonly Menu _menu;
    private TopLevel? _topLevel;
    private CancellationTokenSource? _monitorCancellation;
    private Point? _lastPointerPosition;
    private int _misses;

    private MenuAutoCloseBehavior(Menu menu)
    {
        _menu = menu;
        _menu.AddHandler(MenuItem.SubmenuOpenedEvent, OnSubmenuOpened);
        _menu.AttachedToVisualTree += OnAttachedToVisualTree;
        _menu.DetachedFromVisualTree += OnDetachedFromVisualTree;

        if (TopLevel.GetTopLevel(_menu) is { } topLevel)
        {
            AttachTopLevel(topLevel);
        }
    }

    public static MenuAutoCloseBehavior Attach(Menu menu)
        => new(menu);

    public void Dispose()
    {
        StopMonitoring();
        _menu.RemoveHandler(MenuItem.SubmenuOpenedEvent, OnSubmenuOpened);
        _menu.AttachedToVisualTree -= OnAttachedToVisualTree;
        _menu.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        DetachTopLevel();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(_menu) is { } topLevel)
        {
            AttachTopLevel(topLevel);
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        StopMonitoring();
        DetachTopLevel();
    }

    private void AttachTopLevel(TopLevel topLevel)
    {
        DetachTopLevel();

        _topLevel = topLevel;
        _topLevel.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        _topLevel.AddHandler(
            InputElement.PointerExitedEvent,
            OnPointerExited,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private void DetachTopLevel()
    {
        if (_topLevel is not { } topLevel)
        {
            return;
        }

        topLevel.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
        topLevel.RemoveHandler(InputElement.PointerExitedEvent, OnPointerExited);
        _topLevel = null;
        _lastPointerPosition = null;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_topLevel is { } topLevel)
        {
            _lastPointerPosition = e.GetPosition(topLevel);
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _lastPointerPosition = null;
    }

    private void OnSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        _misses = 0;

        if (e.Source is MenuItem item)
        {
            ConfigurePopup(item);
        }

        if (_lastPointerPosition is not { } position || !IsPointerWithinMenu(position))
        {
            return;
        }

        StartMonitoring();
    }

    private void ConfigurePopup(MenuItem item)
    {
        var popup = FindPopup(item);
        if (popup is not null)
        {
            popup.OverlayInputPassThroughElement = _menu;
        }
    }

    private static Popup? FindPopup(MenuItem item)
        => item.GetVisualDescendants()
            .OfType<Popup>()
            .FirstOrDefault(popup => popup.Name == "PART_Popup");

    private void StartMonitoring()
    {
        StopMonitoring();
        _monitorCancellation = new CancellationTokenSource();
        _ = MonitorAsync(_monitorCancellation.Token);
    }

    private void StopMonitoring()
    {
        if (_monitorCancellation is { } cancellation)
        {
            _monitorCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(CheckInterval, cancellationToken);

                if (!HasOpenSubmenu())
                {
                    return;
                }

                if (_lastPointerPosition is not { } position ||
                    IsPointerWithinMenu(position))
                {
                    _misses = 0;
                    continue;
                }

                _misses++;
                if (_misses < CloseAfterMisses)
                {
                    continue;
                }

                _misses = 0;
                _menu.Close();
                return;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private bool HasOpenSubmenu()
        => _menu.Items.OfType<MenuItem>().Any(item => item.IsSubMenuOpen);

    private bool IsPointerWithinMenu(Point position)
    {
        if (_menu.IsPointerOver || IsWithin(_menu, position))
        {
            return true;
        }

        foreach (var item in _menu.Items.OfType<MenuItem>())
        {
            if (!item.IsSubMenuOpen)
            {
                continue;
            }

            var popup = FindPopup(item);

            if (popup?.Child is { } child &&
                (child.IsPointerOver || IsWithin(child, position)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWithin(Control control, Point position)
    {
        if (_topLevel is null ||
            control.Bounds.Width <= 0 ||
            control.Bounds.Height <= 0)
        {
            return false;
        }

        var origin = control.TranslatePoint(new Point(0, 0), _topLevel);
        return origin is { } topLeft &&
               new Rect(topLeft, control.Bounds.Size).Contains(position);
    }
}
