using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Weftline.Graph.Layout;
using Weftline.Graph.Rendering;
using Weftline.Models;

namespace Weftline.Graph.Controls;

/// <summary>
/// 自定义绘制的剧情图控件。它只消费 GraphProjection，不直接接触 Parser/AST。
///
/// 用法：
/// <code>
/// var projection = GraphProjectionBuilder.Build(model, graph, analysis);
/// var view = new StoryGraphView { Projection = projection };
/// </code>
///
/// A custom-drawn story graph control. It consumes GraphProjection only and
/// does not touch the parser or AST.
/// </summary>
public sealed class StoryGraphView : Control
{
    public static readonly StyledProperty<GraphProjection?> ProjectionProperty =
        AvaloniaProperty.Register<StoryGraphView, GraphProjection?>(nameof(Projection));

    private const double GridSize = 32;
    private const double ArrowLength = 9;
    private const double ArrowHalfWidth = 4.5;
    private const double ChapterAnchorRadius = 6.5;
    private const double MinZoom = 0.4;
    private const double MaxZoom = 2.5;
    private const double ResizeHandleSize = 12;
    private const double MinModuleWidth = 80;
    private const double MinModuleHeight = 30;
    private const double MinChapterWidth = 140;
    private const double MinChapterHeight = 90;

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor ResizeCursor = new(StandardCursorType.BottomRightCorner);
    private static readonly Dictionary<(string Text, double Size, bool Bold, IBrush Brush), FormattedText> TextCache = new();

    private readonly Dictionary<string, Rect> _moduleBounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect> _chapterBounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect> _previewModuleBounds = new(StringComparer.Ordinal);

    private GraphLayout? _layout;
    private ScrollViewer? _scrollViewer;
    private double _zoom = 1;
    private string? _previewChapterId;
    private Rect? _previewChapterBounds;
    private Rect? _previewEndBounds;
    private Rect? _endBoundsOverride;
    private Rect _dragEndStartBounds;
    private bool _draggingEnd;
    private bool _selectedEnd;
    private bool _hoveredEnd;
    private GraphLayout? _baseLayout;
    private string? _hoveredModuleId;
    private string? _hoveredChapterId;
    private string? _selectedModuleId;
    private string? _selectedChapterId;
    private string? _dragModuleId;
    private string? _dragChapterId;
    private Rect _dragStartBounds;
    private Rect _dragModuleChapterBounds;
    private Point _dragStart;
    private Dictionary<string, Rect> _chapterDragModuleStarts = new(StringComparer.Ordinal);
    private bool _dragging;
    private bool _resizing;
    private bool _draggingChapter;
    private bool _resizingChapter;
    private DateTime _lastLayoutRefresh;

    static StoryGraphView()
    {
        AffectsMeasure<StoryGraphView>(ProjectionProperty);
        AffectsRender<StoryGraphView>(ProjectionProperty);
    }

    public GraphProjection? Projection
    {
        get => GetValue(ProjectionProperty);
        set => SetValue(ProjectionProperty, value);
    }

    /// <summary>
    /// 当前缩放比例。滚轮缩放，范围 0.4–2.5。
    /// Current zoom factor. Wheel zoom, clamped to 0.4–2.5.
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        private set
        {
            var zoom = Math.Clamp(value, MinZoom, MaxZoom);

            if (Math.Abs(zoom - _zoom) < 0.0001)
            {
                return;
            }

            _zoom = zoom;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Projection is not { } projection)
        {
            _layout = null;
            _baseLayout = null;
            return default;
        }

        _baseLayout ??= GraphLayoutEngine.Build(projection);

        _layout = GraphLayoutEngine.ApplyOverrides(
            projection,
            _baseLayout,
            new GraphLayoutOptions(_moduleBounds, _chapterBounds, _endBoundsOverride));

        _scrollViewer ??= FindScrollViewer();

        var viewport = _scrollViewer?.Viewport ?? default;

        return new Size(
            Math.Max(_layout.Size.Width * Zoom, viewport.Width),
            Math.Max(_layout.Size.Height * Zoom, viewport.Height));
    }

    private void RefreshLayout(bool force = false)
    {
        if (Projection is not { } projection)
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (!force && (now - _lastLayoutRefresh).TotalMilliseconds < 16)
        {
            return;
        }

        _lastLayoutRefresh = now;

        var previousSize = _layout?.Size ?? default;

        _baseLayout ??= GraphLayoutEngine.Build(projection);

        _layout = GraphLayoutEngine.ApplyOverrides(
            projection,
            _baseLayout,
            new GraphLayoutOptions(_moduleBounds, _chapterBounds, _endBoundsOverride));

        if (_layout.Size.Width > previousSize.Width ||
            _layout.Size.Height > previousSize.Height)
        {
            InvalidateMeasure();
        }

        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ProjectionProperty)
        {
            _baseLayout = null;
            _previewModuleBounds.Clear();
            _previewChapterId = null;
            _previewChapterBounds = null;
            _previewEndBounds = null;
            _hoveredModuleId = null;
            _hoveredChapterId = null;
            _hoveredEnd = false;
            _selectedModuleId = null;
            _selectedChapterId = null;
            _selectedEnd = false;
            _dragModuleId = null;
            _dragChapterId = null;
            _dragging = false;
            _resizing = false;
            _draggingChapter = false;
            _resizingChapter = false;
            _draggingEnd = false;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _scrollViewer = FindScrollViewer();

        if (_scrollViewer is not null)
        {
            _scrollViewer.SizeChanged += OnScrollViewerSizeChanged;
        }

        InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_scrollViewer is not null)
        {
            _scrollViewer.SizeChanged -= OnScrollViewerSizeChanged;
            _scrollViewer = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e)
        => InvalidateMeasure();

    private ScrollViewer? FindScrollViewer()
    {
        var parent = this.GetVisualParent();

        while (parent is not null)
        {
            if (parent is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            parent = parent.GetVisualParent();
        }

        return null;
    }

    /// <summary>
    /// 清除所有手工拖动/缩放，恢复自动布局。
    /// Clears all manual moves/resizes and restores the automatic layout.
    /// </summary>
    public void ResetLayout()
    {
        _moduleBounds.Clear();
        _chapterBounds.Clear();
        _previewModuleBounds.Clear();
        _previewChapterId = null;
        _previewChapterBounds = null;
        _previewEndBounds = null;
        _endBoundsOverride = null;
        _hoveredModuleId = null;
        _hoveredChapterId = null;
        _hoveredEnd = false;
        _selectedModuleId = null;
        _selectedChapterId = null;
        _selectedEnd = false;
        Zoom = 1;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// 用户完成一次拖动或缩放后触发。
    /// Raised after the user finishes a drag or resize.
    /// </summary>
    public event EventHandler? LayoutChanged;

    public override void Render(DrawingContext context)
    {
        if (_layout is not { } layout)
        {
            return;
        }

        context.FillRectangle(GraphTheme.CanvasBackground, new Rect(Bounds.Size));

        using (context.PushTransform(Matrix.CreateScale(Zoom, Zoom)))
        {
            var gridSize = new Size(Bounds.Width / Zoom, Bounds.Height / Zoom);
            DrawGrid(context, gridSize);

            DrawChapters(context, layout);
            DrawBoundaryLinks(context, layout);
            DrawModuleWires(context, layout);
            DrawChapterWires(context, layout);
            DrawEnd(context, layout);
            DrawModules(context, layout);
            DrawChapterAnchors(context, layout);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _previewModuleBounds.Clear();
        _previewChapterId = null;
        _previewChapterBounds = null;
        _previewEndBounds = null;

        var point = ToLayoutPoint(e.GetPosition(this));
        var module = HitTestModule(point);

        if (module is not null)
        {
            var moduleId = module.Node.ModuleId;

            _selectedModuleId = moduleId;
            _selectedChapterId = null;
            _selectedEnd = false;
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
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

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
            _hoveredModuleId = hoveredModule;
            _hoveredChapterId = hoveredChapter;
            _hoveredEnd = hoveredEnd;
            InvalidateVisual();
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

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_dragging || _draggingChapter || _draggingEnd)
        {
            return;
        }

        if (_hoveredModuleId is not null || _hoveredChapterId is not null || _hoveredEnd)
        {
            _hoveredModuleId = null;
            _hoveredChapterId = null;
            _hoveredEnd = false;
            InvalidateVisual();
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

    private Point ToLayoutPoint(Point point)
        => new(point.X / Zoom, point.Y / Zoom);

    private Rect GetModuleBounds(GraphModuleLayout module)
        => _previewModuleBounds.TryGetValue(module.Node.ModuleId, out var preview)
            ? preview
            : module.Bounds;

    private Rect GetChapterBounds(GraphChapterLayout chapter)
        => string.Equals(_previewChapterId, chapter.Chapter.ChapterId, StringComparison.Ordinal) &&
           _previewChapterBounds is { } preview
            ? preview
            : chapter.Bounds;

    private Rect GetEndBounds()
        => _previewEndBounds ??
           _endBoundsOverride ??
           _layout?.EndBounds ??
           default;

    private IEnumerable<GraphModuleLayout> EnumerateChapterModules(string chapterId)
    {
        if (_layout is null)
        {
            return [];
        }

        return _layout.Modules.Where(module =>
            string.Equals(module.Node.ChapterId, chapterId, StringComparison.Ordinal));
    }

    private GraphModuleLayout? HitTestModule(Point point)
    {
        if (_layout is null || _layout.Modules.Count == 0)
        {
            return null;
        }

        for (var index = _layout.Modules.Count - 1; index >= 0; index--)
        {
            if (_layout.Modules[index].Bounds.Contains(point))
            {
                return _layout.Modules[index];
            }
        }

        return null;
    }

    private GraphChapterLayout? HitTestChapter(Point point)
    {
        if (_layout is null || _layout.Chapters.Count == 0)
        {
            return null;
        }

        for (var index = _layout.Chapters.Count - 1; index >= 0; index--)
        {
            if (_layout.Chapters[index].Bounds.Contains(point))
            {
                return _layout.Chapters[index];
            }
        }

        return null;
    }

    private bool HitTestEnd(Point point)
        => _layout is not null && GetEndBounds().Contains(point);

    private static bool IsResizeHandle(Rect bounds, Point point)
        => new Rect(
            bounds.Right - ResizeHandleSize,
            bounds.Bottom - ResizeHandleSize,
            ResizeHandleSize,
            ResizeHandleSize).Contains(point);

    private static bool IsChapterHeader(Rect bounds, Point point)
        => new Rect(
            bounds.X,
            bounds.Y,
            bounds.Width,
            GraphLayoutEngine.ChapterHeader).Contains(point);

    private static bool IsChapterResizeHandle(Rect bounds, Point point)
        => new Rect(
            bounds.Right - ResizeHandleSize,
            bounds.Bottom - ResizeHandleSize,
            ResizeHandleSize,
            ResizeHandleSize).Contains(point);

    private static Rect ClampModuleBounds(Rect proposed, Rect chapterBounds)
    {
        if (chapterBounds.Width <= 0 || chapterBounds.Height <= 0)
        {
            return proposed;
        }

        const double inset = 4;

        var minX = chapterBounds.X + inset;
        var maxX = chapterBounds.Right - proposed.Width - inset;
        var minY = chapterBounds.Y + GraphLayoutEngine.ChapterHeader + inset;
        var maxY = chapterBounds.Bottom - proposed.Height - inset;

        return new Rect(
            Math.Clamp(proposed.X, minX, Math.Max(minX, maxX)),
            Math.Clamp(proposed.Y, minY, Math.Max(minY, maxY)),
            proposed.Width,
            proposed.Height);
    }

    private static Rect ClampModuleSize(
        Rect start,
        double dx,
        double dy,
        Rect chapterBounds)
    {
        const double inset = 4;

        var maxWidth = chapterBounds.Width <= 0
            ? start.Width + dx
            : chapterBounds.Right - start.X - inset;

        var maxHeight = chapterBounds.Height <= 0
            ? start.Height + dy
            : chapterBounds.Bottom - start.Y - inset;

        return new Rect(
            start.X,
            start.Y,
            Math.Clamp(start.Width + dx, MinModuleWidth, Math.Max(MinModuleWidth, maxWidth)),
            Math.Clamp(start.Height + dy, MinModuleHeight, Math.Max(MinModuleHeight, maxHeight)));
    }

    private static void DrawGrid(DrawingContext context, Size size)
    {
        for (var x = 0d; x <= size.Width; x += GridSize)
        {
            context.DrawLine(
                GraphTheme.GridPen,
                new Point(x, 0),
                new Point(x, size.Height));
        }

        for (var y = 0d; y <= size.Height; y += GridSize)
        {
            context.DrawLine(
                GraphTheme.GridPen,
                new Point(0, y),
                new Point(size.Width, y));
        }
    }

    private void DrawChapters(DrawingContext context, GraphLayout layout)
    {
        foreach (var chapter in layout.Chapters)
        {
            var bounds = GetChapterBounds(chapter);

            context.DrawRectangle(
                GraphTheme.ChapterShadow,
                null,
                new Rect(bounds.X + 2, bounds.Y + 3, bounds.Width, bounds.Height),
                14,
                14);

            context.DrawRectangle(
                GraphTheme.ChapterFill,
                GraphTheme.ChapterPen,
                bounds,
                14,
                14);

            var isEntry = chapter.Chapter.Flags.HasFlag(GraphChapterFlags.Entry);

            context.DrawRectangle(
                isEntry ? GraphTheme.ChapterEntryHeaderFill : GraphTheme.ChapterHeaderFill,
                null,
                new Rect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 34),
                13,
                13);

            context.DrawRectangle(
                isEntry ? GraphTheme.EntryAccent : GraphTheme.AccentSoft,
                null,
                new Rect(bounds.X + 14, bounds.Y + 9, 3, 22),
                1.5,
                1.5);

            DrawCenteredText(
                context,
                chapter.Chapter.Title,
                new Rect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 34),
                13,
                GraphTheme.ChapterTitle,
                bold: true);

            var isSelected = string.Equals(
                _selectedChapterId,
                chapter.Chapter.ChapterId,
                StringComparison.Ordinal);

            var isHovered = string.Equals(
                _hoveredChapterId,
                chapter.Chapter.ChapterId,
                StringComparison.Ordinal);

            if (!isSelected && !isHovered)
            {
                continue;
            }

            context.DrawRectangle(
                null,
                isSelected ? GraphTheme.SelectedPen : GraphTheme.HoverPen,
                new Rect(
                    bounds.X - 2,
                    bounds.Y - 2,
                    bounds.Width + 4,
                    bounds.Height + 4),
                16,
                16);

            if (!isSelected)
            {
                continue;
            }

            context.DrawRectangle(
                GraphTheme.ResizeHandleFill,
                GraphTheme.ExitWirePen,
                new Rect(
                    bounds.Right - 12,
                    bounds.Bottom - 12,
                    10,
                    10),
                2,
                2);
        }
    }

    private void DrawChapterAnchors(DrawingContext context, GraphLayout layout)
    {
        foreach (var chapter in layout.Chapters)
        {
            var bounds = GetChapterBounds(chapter);
            var centerY = bounds.Y + (bounds.Height / 2);
            var chapterId = chapter.Chapter.ChapterId;

            if (EnumerateChapterModules(chapterId)
                .Any(module => layout.ChapterEntryModules.Contains(module.Node.ModuleId)))
            {
                DrawAnchor(context, new Point(bounds.X, centerY), GraphTheme.EntryAccent);
            }

            if (EnumerateChapterModules(chapterId)
                .Any(module => layout.ChapterExitModules.Contains(module.Node.ModuleId)))
            {
                DrawAnchor(context, new Point(bounds.Right, centerY), GraphTheme.ExitAccent);
            }
        }
    }

    private static void DrawAnchor(DrawingContext context, Point center, IBrush fill)
    {
        context.DrawEllipse(
            fill,
            GraphTheme.ChapterAnchorPen,
            center,
            ChapterAnchorRadius,
            ChapterAnchorRadius);
    }

    private void DrawBoundaryLinks(DrawingContext context, GraphLayout layout)
    {
        foreach (var chapter in layout.Chapters)
        {
            var bounds = GetChapterBounds(chapter);
            var centerY = bounds.Y + (bounds.Height / 2);
            var entryAnchor = new Point(bounds.X, centerY);
            var exitAnchor = new Point(bounds.Right, centerY);

            foreach (var module in EnumerateChapterModules(chapter.Chapter.ChapterId))
            {
                var moduleBounds = GetModuleBounds(module);
                var moduleCenterY = moduleBounds.Y + (moduleBounds.Height / 2);

                if (layout.ChapterEntryModules.Contains(module.Node.ModuleId))
                {
                    var end = new Point(moduleBounds.X, moduleCenterY);
                    var dy = end.Y - entryAnchor.Y;
                    var verticalOffset = Math.Clamp(Math.Abs(dy) * 0.25, 0, 40) * (dy >= 0 ? 1 : -1);

                    DrawWire(
                        context,
                        entryAnchor,
                        new Point(bounds.X + 42, entryAnchor.Y),
                        new Point(end.X - 42, end.Y - verticalOffset),
                        end,
                        GraphTheme.EntryWirePen,
                        GraphTheme.EntryWirePen,
                        GraphTheme.EntryAccent,
                        2);
                }

                if (layout.ChapterExitModules.Contains(module.Node.ModuleId))
                {
                    var start = new Point(moduleBounds.Right, moduleCenterY);
                    var dy = exitAnchor.Y - start.Y;
                    var verticalOffset = Math.Clamp(Math.Abs(dy) * 0.25, 0, 40) * (dy >= 0 ? 1 : -1);

                    DrawWire(
                        context,
                        start,
                        new Point(start.X + 42, start.Y),
                        new Point(exitAnchor.X - 42, exitAnchor.Y - verticalOffset),
                        exitAnchor,
                        GraphTheme.ExitWirePen,
                        GraphTheme.ExitWirePen,
                        GraphTheme.ExitAccent,
                        ChapterAnchorRadius + 3);
                }
            }
        }
    }

    private void DrawModuleWires(DrawingContext context, GraphLayout layout)
    {
        var modules = layout.Modules.ToDictionary(
            static module => module.Node.ModuleId,
            StringComparer.Ordinal);

        foreach (var edge in layout.ModuleEdges)
        {
            if (!modules.TryGetValue(edge.FromModule, out var from) ||
                !modules.TryGetValue(edge.ToModule, out var to))
            {
                continue;
            }

            var fromBounds = GetModuleBounds(from);
            var toBounds = GetModuleBounds(to);

            var start = new Point(
                fromBounds.X + (fromBounds.Width / 2),
                fromBounds.Bottom);

            var end = new Point(
                toBounds.X + (toBounds.Width / 2),
                toBounds.Y);

            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var vertical = dy >= 0 ? 1 : -1;
            var horizontal = Math.Clamp(Math.Abs(dx) * 0.35, 0, 80);
            var horizontalSign = dx >= 0 ? 1 : -1;
            var handle = Math.Clamp(Math.Abs(dy) * 0.45, 26, 90);

            var control1 = new Point(
                start.X + (horizontal * horizontalSign),
                start.Y + (handle * vertical));

            var control2 = new Point(
                end.X - (horizontal * horizontalSign),
                end.Y - (handle * vertical));

            DrawWire(
                context,
                start,
                control1,
                control2,
                end,
                GraphTheme.ModuleWireGlowPen,
                GraphTheme.ModuleWirePen,
                GraphTheme.Wire);
        }
    }

    private void DrawChapterWires(DrawingContext context, GraphLayout layout)
    {
        var chaptersById = layout.Chapters.ToDictionary(
            static chapter => chapter.Chapter.ChapterId,
            StringComparer.Ordinal);

        foreach (var edgeLayout in layout.ChapterEdges)
        {
            var edge = edgeLayout.Edge;

            if (!chaptersById.TryGetValue(edge.FromChapter, out var from))
            {
                continue;
            }

            var fromBounds = GetChapterBounds(from);
            var start = new Point(
                fromBounds.Right,
                fromBounds.Y + (fromBounds.Height / 2));

            Point end;

            if (edge.ToChapter == "END")
            {
                var endBounds = GetEndBounds();
                end = new Point(
                    endBounds.X,
                    endBounds.Y + (endBounds.Height / 2));
            }
            else if (chaptersById.TryGetValue(edge.ToChapter, out var to))
            {
                var toBounds = GetChapterBounds(to);
                end = new Point(
                    toBounds.X,
                    toBounds.Y + (toBounds.Height / 2));
            }
            else
            {
                continue;
            }

            var isBackEdge = end.X < start.X;

            IBrush arrow = isBackEdge ? GraphTheme.Warning : GraphTheme.ChapterWire;
            IPen glow = isBackEdge ? GraphTheme.BackEdgePen : GraphTheme.ChapterWireGlowPen;
            IPen core = isBackEdge ? GraphTheme.BackEdgePen : GraphTheme.ChapterWirePen;

            if (isBackEdge)
            {
                var top = Math.Min(start.Y, end.Y) - 96;

                DrawWire(
                    context,
                    start,
                    new Point(start.X + 80, top),
                    new Point(end.X - 80, top),
                    end,
                    glow,
                    core,
                    arrow,
                    ChapterAnchorRadius + 3);
            }
            else
            {
                var handle = Math.Clamp(
                    Math.Abs(end.X - start.X) * 0.4,
                    70,
                    190);

                var dy = end.Y - start.Y;
                var verticalOffset = Math.Clamp(Math.Abs(dy) * 0.25, 0, 60) * (dy >= 0 ? 1 : -1);

                DrawWire(
                    context,
                    start,
                    new Point(start.X + handle, start.Y + (verticalOffset * 0.5)),
                    new Point(end.X - handle, end.Y - verticalOffset),
                    end,
                    glow,
                    core,
                    arrow,
                    ChapterAnchorRadius + 3);
            }
        }
    }

    private void DrawEnd(DrawingContext context, GraphLayout layout)
    {
        var bounds = GetEndBounds();

        context.DrawRectangle(
            GraphTheme.ChapterShadow,
            null,
            new Rect(
                bounds.X + 2,
                bounds.Y + 3,
                bounds.Width,
                bounds.Height),
            12,
            12);

        context.DrawRectangle(
            GraphTheme.EndFill,
            GraphTheme.EndPen,
            bounds,
            12,
            12);

        DrawCenteredText(
            context,
            "END",
            bounds,
            12,
            GraphTheme.EndText,
            bold: true);

        if (!_selectedEnd && !_hoveredEnd)
        {
            return;
        }

        context.DrawRectangle(
            null,
            _selectedEnd ? GraphTheme.SelectedPen : GraphTheme.HoverPen,
            new Rect(
                bounds.X - 2,
                bounds.Y - 2,
                bounds.Width + 4,
                bounds.Height + 4),
            14,
            14);
    }

    private void DrawModules(DrawingContext context, GraphLayout layout)
    {
        foreach (var module in layout.Modules)
        {
            DrawModule(context, module);
        }
    }

    private void DrawModule(
        DrawingContext context,
        GraphModuleLayout module)
    {
        var node = module.Node;
        var bounds = GetModuleBounds(module);

        context.DrawRectangle(
            GraphTheme.ModuleShadow,
            null,
            new Rect(bounds.X + 1, bounds.Y + 2, bounds.Width, bounds.Height),
            9,
            9);

        var pen = node.Flags.HasFlag(GraphModuleFlags.Unreachable)
            ? GraphTheme.UnreachablePen
            : GraphTheme.ModulePen;

        context.DrawRectangle(
            GraphTheme.ModuleFill,
            pen,
            bounds,
            9,
            9);

        DrawCenteredText(
            context,
            node.ModuleId,
            bounds,
            12,
            GraphTheme.ModuleTitle,
            bold: true);

        var isSelected = string.Equals(_selectedModuleId, node.ModuleId, StringComparison.Ordinal);
        var isHovered = string.Equals(_hoveredModuleId, node.ModuleId, StringComparison.Ordinal);

        if (!isSelected && !isHovered)
        {
            return;
        }

        context.DrawRectangle(
            null,
            isSelected ? GraphTheme.SelectedPen : GraphTheme.HoverPen,
            new Rect(
                bounds.X - 2,
                bounds.Y - 2,
                bounds.Width + 4,
                bounds.Height + 4),
            10,
            10);

        if (!isSelected)
        {
            return;
        }

        context.DrawRectangle(
            GraphTheme.ResizeHandleFill,
            GraphTheme.ExitWirePen,
            new Rect(
                bounds.Right - 10,
                bounds.Bottom - 10,
                8,
                8),
            2,
            2);
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        Point origin,
        double size,
        IBrush brush,
        bool bold = false)
    {
        context.DrawText(GetFormattedText(text, size, brush, bold), origin);
    }

    private static void DrawCenteredText(
        DrawingContext context,
        string text,
        Rect bounds,
        double size,
        IBrush brush,
        bool bold = false)
    {
        var formatted = GetFormattedText(text, size, brush, bold);

        context.DrawText(
            formatted,
            new Point(
                bounds.X + ((bounds.Width - formatted.Width) / 2),
                bounds.Y + ((bounds.Height - formatted.Height) / 2)));
    }

    private static FormattedText GetFormattedText(
        string text,
        double size,
        IBrush brush,
        bool bold)
    {
        var key = (text, size, bold, brush);

        if (TextCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            bold ? GraphTheme.BoldTypeface : GraphTheme.Typeface,
            size,
            brush);

        TextCache[key] = formatted;
        return formatted;
    }

    private static void DrawWire(
        DrawingContext context,
        Point start,
        Point control1,
        Point control2,
        Point end,
        IPen glowPen,
        IPen corePen,
        IBrush arrowBrush,
        double arrowInset = 5)
    {
        DrawCurve(context, start, control1, control2, end, glowPen);
        DrawCurve(context, start, control1, control2, end, corePen);
        DrawArrow(context, control2, end, arrowBrush, arrowInset);
    }

    private static void DrawCurve(
        DrawingContext context,
        Point start,
        Point control1,
        Point control2,
        Point end,
        IPen pen)
    {
        var geometry = new StreamGeometry();

        using (var stream = geometry.Open())
        {
            stream.BeginFigure(start, false);
            stream.CubicBezierTo(control1, control2, end);
            stream.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawArrow(
        DrawingContext context,
        Point control,
        Point end,
        IBrush brush,
        double inset)
    {
        var dx = end.X - control.X;
        var dy = end.Y - control.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < 0.001)
        {
            return;
        }

        var ux = dx / length;
        var uy = dy / length;

        var tip = new Point(end.X - (ux * inset), end.Y - (uy * inset));
        var baseCenter = new Point(
            tip.X - (ux * ArrowLength),
            tip.Y - (uy * ArrowLength));

        var px = -uy * ArrowHalfWidth;
        var py = ux * ArrowHalfWidth;

        var geometry = new StreamGeometry();

        using (var stream = geometry.Open())
        {
            stream.BeginFigure(tip, true);
            stream.LineTo(new Point(baseCenter.X + px, baseCenter.Y + py));
            stream.LineTo(new Point(baseCenter.X - px, baseCenter.Y - py));
            stream.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }
}
