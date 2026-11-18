using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Weftline.App.Features.Graph.Layout;
using Weftline.App.Features.Graph.Rendering;
using Weftline.Core;
using Weftline.Core.Graph.Analysis;

namespace Weftline.App.Features.Graph.Controls;

/// <summary>
/// 自定义绘制的剧情图控件。它只消费 GraphProjection，不直接接触 Parser/AST。
///
/// 用法：
/// <code>
/// var projection = response.Projection?.ToGraphProjection();
/// var view = new StoryGraphView { Projection = projection };
/// </code>
///
/// A custom-drawn story graph control. It consumes GraphProjection only and
/// does not touch the parser or AST.
/// </summary>
public sealed partial class StoryGraphView : Control
{
    public static readonly StyledProperty<GraphProjection?> ProjectionProperty =
        AvaloniaProperty.Register<StoryGraphView, GraphProjection?>(nameof(Projection));

    public static readonly StyledProperty<GraphFilterSelection?> FilterProperty =
        AvaloniaProperty.Register<StoryGraphView, GraphFilterSelection?>(nameof(Filter));

    private const double GridSize = 32;
    private const double DimOpacity = 0.22;
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
    private const double EdgeHitTolerance = 8;

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor ResizeCursor = new(StandardCursorType.BottomRightCorner);
    private static readonly Cursor PanCursor = new(StandardCursorType.SizeAll);
    private static readonly Dictionary<(string Text, double Size, bool Bold, IBrush Brush), FormattedText> TextCache = new();

    private readonly Dictionary<string, Rect> _moduleBounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect> _chapterBounds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Rect> _previewModuleBounds = new(StringComparer.Ordinal);
    private GraphPathHighlight _pathHighlight = GraphPathHighlight.Empty;
    private GraphFilterMatch _filterMatch = GraphFilterMatch.Empty;

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
    private bool _hoverRedrawScheduled;
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
    private bool _panning;
    private Point _panStart;
    private Vector _panStartOffset;
    private DateTime _lastLayoutRefresh;

    static StoryGraphView()
    {
        AffectsMeasure<StoryGraphView>(ProjectionProperty);
        AffectsRender<StoryGraphView>(ProjectionProperty);
        AffectsRender<StoryGraphView>(FilterProperty);
    }
    public GraphProjection? Projection
    {
        get => GetValue(ProjectionProperty);
        set => SetValue(ProjectionProperty, value);
    }

    /// <summary>
    /// 按角色/场景/变量筛选；空值或空集合表示不筛选。
    /// Filters by character/scene/variable; null or empty means no filtering.
    /// </summary>
    public GraphFilterSelection? Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
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
            var oldProjection = change.OldValue as GraphProjection;
            var newProjection = change.NewValue as GraphProjection;
            var sameStructure = HasSameStructure(oldProjection, newProjection);
            var hadModuleHover = _hoveredModuleId is not null;

            if (!sameStructure)
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

            UpdatePathHighlight();
            UpdateFilterMatches();

            if (hadModuleHover)
            {
                ModuleHoverChanged?.Invoke(
                    this,
                    new ModuleHoverChangedEventArgs(
                        _hoveredModuleId is { } hoveredId
                            ? FindCurrentModule(hoveredId)
                            : null));
            }

            InvalidateVisual();
            return;
        }

        if (change.Property == FilterProperty)
        {
            UpdateFilterMatches();
            InvalidateVisual();
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
    /// 用户完成一次拖动或缩放后触发。
    /// Raised after the user finishes a drag or resize.
    /// </summary>
    public event EventHandler? LayoutChanged;

    /// <summary>
    /// 双击模块时触发。
    /// Raised when a module is double-clicked.
    /// </summary>
    public event EventHandler<ModuleActivatedEventArgs>? ModuleActivated;

    /// <summary>
    /// 双击模块边时触发。
    /// Raised when a module edge is double-clicked.
    /// </summary>
    public event EventHandler<ModuleEdgeActivatedEventArgs>? ModuleEdgeActivated;

    /// <summary>
    /// 双击章节聚合边时触发。
    /// Raised when a chapter edge is double-clicked.
    /// </summary>
    public event EventHandler<ChapterEdgeActivatedEventArgs>? ChapterEdgeActivated;

    /// <summary>
    /// 悬停模块变化时触发；离开模块时参数为 null。
    /// Raised when the hovered module changes; null when leaving modules.
    /// </summary>
    public event EventHandler<ModuleHoverChangedEventArgs>? ModuleHoverChanged;

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
}
