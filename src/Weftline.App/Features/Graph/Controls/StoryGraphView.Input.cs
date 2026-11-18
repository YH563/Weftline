using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Weftline.App.Features.Graph.Layout;
using Weftline.App.Features.Graph.Rendering;
using Weftline.Core;
using Weftline.Core.Graph.Analysis;

namespace Weftline.App.Features.Graph.Controls;

public sealed partial class StoryGraphView
{
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsMiddleButtonPressed)
        {
            var scrollViewer = _scrollViewer ?? FindScrollViewer();
            if (scrollViewer is not null)
            {
                _panning = true;
                _panStart = e.GetPosition(scrollViewer);
                _panStartOffset = scrollViewer.Offset;
                e.Pointer.Capture(this);
                Cursor = PanCursor;
                e.Handled = true;
            }

            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        _previewModuleBounds.Clear();
        _previewChapterId = null;
        _previewChapterBounds = null;
        _previewEndBounds = null;

        var point = ToLayoutPoint(e.GetPosition(this));

        if (e.ClickCount == 2 && TryActivate(point))
        {
            e.Handled = true;
            return;
        }

        var module = HitTestModule(point);

        if (module is not null)
        {
            var moduleId = module.Node.ModuleId;

            _selectedModuleId = moduleId;
            _selectedChapterId = null;
            _selectedEnd = false;
            UpdatePathHighlight();
            _dragModuleId = moduleId;
            _dragStart = point;
            _dragStartBounds = _moduleBounds.TryGetValue(moduleId, out var manual)
                ? manual
                : module.Bounds;
            _dragModuleChapterBounds = _layout?.FindChapter(module.Node.ChapterId)?.Bounds ?? default;
            _resizing = IsResizeHandle(module.Bounds, point);
            _dragging = true;

            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        var chapter = HitTestChapter(point);

        if (chapter is not null &&
            (IsChapterHeader(chapter.Bounds, point) ||
             IsChapterResizeHandle(chapter.Bounds, point)))
        {
            var chapterId = chapter.Chapter.ChapterId;

            _selectedChapterId = chapterId;
            _selectedModuleId = null;
            _selectedEnd = false;
            UpdatePathHighlight();
            _dragChapterId = chapterId;
            _dragStart = point;
            _dragStartBounds = _chapterBounds.TryGetValue(chapterId, out var manualChapter)
                ? manualChapter
                : chapter.Bounds;
            _resizingChapter = IsChapterResizeHandle(chapter.Bounds, point);
            _draggingChapter = true;

            _chapterDragModuleStarts = new Dictionary<string, Rect>(StringComparer.Ordinal);

            foreach (var chapterModule in EnumerateChapterModules(chapterId))
            {
                _chapterDragModuleStarts[chapterModule.Node.ModuleId] =
                    _moduleBounds.TryGetValue(chapterModule.Node.ModuleId, out var moduleBounds)
                        ? moduleBounds
                        : chapterModule.Bounds;
            }

            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (HitTestEnd(point))
        {
            _selectedModuleId = null;
            _selectedChapterId = null;
            _selectedEnd = true;
            UpdatePathHighlight();
            _draggingEnd = true;
            _dragStart = point;
            _dragEndStartBounds = GetEndBounds();

            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        _selectedModuleId = null;
        _selectedChapterId = null;
        _selectedEnd = false;
        UpdatePathHighlight();
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_panning)
        {
            var scrollViewer = _scrollViewer ?? FindScrollViewer();
            if (scrollViewer is not null)
            {
                var current = e.GetPosition(scrollViewer);
                var delta = _panStart - current;
                var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
                var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
                var x = Math.Clamp(_panStartOffset.X + delta.X, 0, maxX);
                var y = Math.Clamp(_panStartOffset.Y + delta.Y, 0, maxY);
                scrollViewer.Offset = new Vector(x, y);
                e.Handled = true;
            }

            return;
        }

        var point = ToLayoutPoint(e.GetPosition(this));

        if (_draggingEnd)
        {
            var dx = point.X - _dragStart.X;
            var dy = point.Y - _dragStart.Y;

            _previewEndBounds = new Rect(
                _dragEndStartBounds.X + dx,
                _dragEndStartBounds.Y + dy,
                _dragEndStartBounds.Width,
                _dragEndStartBounds.Height);

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_draggingChapter && _dragChapterId is { } dragChapterId)
        {
            var dx = point.X - _dragStart.X;
            var dy = point.Y - _dragStart.Y;

            Rect bounds;

            if (_resizingChapter)
            {
                bounds = new Rect(
                    _dragStartBounds.X,
                    _dragStartBounds.Y,
                    Math.Max(MinChapterWidth, _dragStartBounds.Width + dx),
                    Math.Max(MinChapterHeight, _dragStartBounds.Height + dy));
            }
            else
            {
                bounds = new Rect(
                    _dragStartBounds.X + dx,
                    _dragStartBounds.Y + dy,
                    _dragStartBounds.Width,
                    _dragStartBounds.Height);
            }

            _previewChapterId = dragChapterId;
            _previewChapterBounds = bounds;
            _previewModuleBounds.Clear();

            if (!_resizingChapter)
            {
                foreach (var (moduleId, moduleBounds) in _chapterDragModuleStarts)
                {
                    _previewModuleBounds[moduleId] = new Rect(
                        moduleBounds.X + dx,
                        moduleBounds.Y + dy,
                        moduleBounds.Width,
                        moduleBounds.Height);
                }
            }

            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (_dragging && _dragModuleId is { } dragModuleId)
        {
            var dx = point.X - _dragStart.X;
            var dy = point.Y - _dragStart.Y;

            var bounds = _resizing
                ? ClampModuleSize(_dragStartBounds, dx, dy, _dragModuleChapterBounds)
                : ClampModuleBounds(
                    new Rect(
                        _dragStartBounds.X + dx,
                        _dragStartBounds.Y + dy,
                        _dragStartBounds.Width,
                        _dragStartBounds.Height),
                    _dragModuleChapterBounds);

            _previewModuleBounds[dragModuleId] = bounds;
            _previewChapterId = null;
            _previewChapterBounds = null;
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        var hoveredModuleLayout = HitTestModule(point);
        var hoveredChapterLayout = hoveredModuleLayout is null
            ? HitTestChapter(point)
            : null;

        var hoveredModule = hoveredModuleLayout?.Node.ModuleId;
        var hoveredChapter = hoveredChapterLayout?.Chapter.ChapterId;
        var hoveredEnd = hoveredModule is null &&
            hoveredChapter is null &&
            HitTestEnd(point);

        if (!string.Equals(hoveredModule, _hoveredModuleId, StringComparison.Ordinal) ||
            !string.Equals(hoveredChapter, _hoveredChapterId, StringComparison.Ordinal) ||
            hoveredEnd != _hoveredEnd)
        {
            var moduleHoverChanged = !string.Equals(
                hoveredModule,
                _hoveredModuleId,
                StringComparison.Ordinal);

            _hoveredModuleId = hoveredModule;
            _hoveredChapterId = hoveredChapter;
            _hoveredEnd = hoveredEnd;
            ScheduleHoverRedraw();

            if (moduleHoverChanged)
            {
                ModuleHoverChanged?.Invoke(
                    this,
                    new ModuleHoverChangedEventArgs(
                        hoveredModule is { } hoveredId
                            ? FindCurrentModule(hoveredId)
                            : null));
            }
        }

        var overResizeHandle =
            (hoveredModuleLayout is not null &&
             IsResizeHandle(hoveredModuleLayout.Bounds, point)) ||
            (hoveredChapterLayout is not null &&
             IsChapterResizeHandle(hoveredChapterLayout.Bounds, point));

        Cursor = overResizeHandle
            ? ResizeCursor
            : hoveredModule is null && hoveredChapter is null && !hoveredEnd
                ? ArrowCursor
                : HandCursor;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_panning)
        {
            _panning = false;
            e.Pointer.Capture(null);
            Cursor = ArrowCursor;
            e.Handled = true;
            return;
        }

        if (!_dragging && !_draggingChapter && !_draggingEnd)
        {
            return;
        }

        foreach (var (moduleId, bounds) in _previewModuleBounds)
        {
            _moduleBounds[moduleId] = bounds;
        }

        if (_previewChapterId is { } chapterId &&
            _previewChapterBounds is { } chapterBounds)
        {
            _chapterBounds[chapterId] = chapterBounds;
        }

        if (_previewEndBounds is { } endBounds)
        {
            _endBoundsOverride = endBounds;
        }

        _previewModuleBounds.Clear();
        _previewChapterId = null;
        _previewChapterBounds = null;
        _previewEndBounds = null;

        e.Pointer.Capture(null);
        _dragging = false;
        _resizing = false;
        _dragModuleId = null;
        _draggingChapter = false;
        _resizingChapter = false;
        _dragChapterId = null;
        _chapterDragModuleStarts.Clear();
        _draggingEnd = false;
        RefreshLayout(force: true);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        if (_panning)
        {
            _panning = false;
            Cursor = ArrowCursor;
        }
    }

    private void ScheduleHoverRedraw()
    {
        if (_hoverRedrawScheduled)
        {
            return;
        }

        _hoverRedrawScheduled = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _hoverRedrawScheduled = false;
                InvalidateVisual();
            },
            DispatcherPriority.Render);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_dragging || _draggingChapter || _draggingEnd)
        {
            return;
        }

        if (_hoveredModuleId is not null || _hoveredChapterId is not null || _hoveredEnd)
        {
            var hadModuleHover = _hoveredModuleId is not null;

            _hoveredModuleId = null;
            _hoveredChapterId = null;
            _hoveredEnd = false;
            ScheduleHoverRedraw();

            if (hadModuleHover)
            {
                ModuleHoverChanged?.Invoke(this, new ModuleHoverChangedEventArgs(null));
            }
        }

        Cursor = ArrowCursor;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.Delta.Y == 0)
        {
            return;
        }

        var oldZoom = Zoom;
        var factor = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
        var newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - oldZoom) < 0.0001)
        {
            e.Handled = true;
            return;
        }

        var scrollViewer = _scrollViewer ?? FindScrollViewer();
        var oldOffset = scrollViewer?.Offset ?? default;
        var position = e.GetPosition(this);
        var layoutPoint = new Point(position.X / oldZoom, position.Y / oldZoom);

        Zoom = newZoom;

        if (scrollViewer is not null)
        {
            var newOffset = new Vector(
                oldOffset.X + (layoutPoint.X * (newZoom - oldZoom)),
                oldOffset.Y + (layoutPoint.Y * (newZoom - oldZoom)));

            Dispatcher.UIThread.Post(() => scrollViewer.Offset = newOffset);
        }

        e.Handled = true;
    }
}
