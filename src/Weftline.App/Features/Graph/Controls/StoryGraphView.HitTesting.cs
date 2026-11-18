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

    private bool TryActivate(Point point)
    {
        if (_layout is not { } layout)
        {
            return false;
        }

        if (HitTestModule(point) is { } module)
        {
            ModuleActivated?.Invoke(
                this,
                new ModuleActivatedEventArgs(
                    FindCurrentModule(module.Node.ModuleId) ?? module.Node));
            return true;
        }

        var chapter = HitTestChapter(point);

        if (chapter is not null && IsChapterHeader(chapter.Bounds, point))
        {
            return false;
        }

        if (HitTestEnd(point))
        {
            return false;
        }

        if (HitTestModuleEdge(layout, point) is { } edge)
        {
            ModuleEdgeActivated?.Invoke(
                this,
                new ModuleEdgeActivatedEventArgs(FindCurrentModuleEdge(edge)));
            return true;
        }

        if (HitTestChapterEdge(layout, point) is { } chapterEdge)
        {
            ChapterEdgeActivated?.Invoke(
                this,
                new ChapterEdgeActivatedEventArgs(FindCurrentChapterEdge(chapterEdge)));
            return true;
        }

        return false;
    }

    private bool TryGetModuleWire(
        IReadOnlyDictionary<string, GraphModuleLayout> modules,
        GraphModuleEdge edge,
        out WireGeometry wire)
    {
        if (!modules.TryGetValue(edge.FromModule, out var from) ||
            !modules.TryGetValue(edge.ToModule, out var to))
        {
            wire = default;
            return false;
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

        wire = new WireGeometry(
            start,
            new Point(
                start.X + (horizontal * horizontalSign),
                start.Y + (handle * vertical)),
            new Point(
                end.X - (horizontal * horizontalSign),
                end.Y - (handle * vertical)),
            end,
            5);
        return true;
    }

    private bool TryGetChapterWire(
        GraphLayout layout,
        IReadOnlyDictionary<string, GraphChapterLayout> chaptersById,
        GraphChapterEdgeLayout edgeLayout,
        out WireGeometry wire)
    {
        var edge = edgeLayout.Edge;

        if (!chaptersById.TryGetValue(edge.FromChapter, out var from))
        {
            wire = default;
            return false;
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
            wire = default;
            return false;
        }

        var arrowInset = ChapterAnchorRadius + 3;

        if (end.X < start.X)
        {
            var top = Math.Min(start.Y, end.Y) - 96;

            wire = new WireGeometry(
                start,
                new Point(start.X + 80, top),
                new Point(end.X - 80, top),
                end,
                arrowInset);
            return true;
        }

        var handle = Math.Clamp(
            Math.Abs(end.X - start.X) * 0.4,
            70,
            190);

        var dy = end.Y - start.Y;
        var verticalOffset = Math.Clamp(Math.Abs(dy) * 0.25, 0, 60) * (dy >= 0 ? 1 : -1);

        wire = new WireGeometry(
            start,
            new Point(start.X + handle, start.Y + (verticalOffset * 0.5)),
            new Point(end.X - handle, end.Y - verticalOffset),
            end,
            arrowInset);
        return true;
    }

    private GraphModuleEdge? HitTestModuleEdge(GraphLayout layout, Point point)
    {
        var modules = layout.Modules.ToDictionary(
            static module => module.Node.ModuleId,
            StringComparer.Ordinal);

        foreach (var edge in layout.ModuleEdges)
        {
            if (TryGetModuleWire(modules, edge, out var wire) &&
                DistanceToWire(point, wire) <= EdgeHitTolerance)
            {
                return edge;
            }
        }

        return null;
    }

    private ChapterEdge? HitTestChapterEdge(GraphLayout layout, Point point)
    {
        var chaptersById = layout.Chapters.ToDictionary(
            static chapter => chapter.Chapter.ChapterId,
            StringComparer.Ordinal);

        foreach (var edgeLayout in layout.ChapterEdges)
        {
            if (TryGetChapterWire(layout, chaptersById, edgeLayout, out var wire) &&
                DistanceToWire(point, wire) <= EdgeHitTolerance)
            {
                return edgeLayout.Edge;
            }
        }

        return null;
    }

    private static double DistanceToWire(Point point, WireGeometry wire)
    {
        const int steps = 24;

        var previous = wire.Start;
        var best = double.MaxValue;

        for (var index = 1; index <= steps; index++)
        {
            var t = index / (double)steps;
            var current = PointOnCubic(t, wire);
            best = Math.Min(best, DistanceToSegment(point, previous, current));
            previous = current;
        }

        return best;
    }

    private static Point PointOnCubic(double t, WireGeometry wire)
    {
        var u = 1 - t;
        var uu = u * u;
        var tt = t * t;

        return new Point(
            (uu * u * wire.Start.X) +
            (3 * uu * t * wire.Control1.X) +
            (3 * u * tt * wire.Control2.X) +
            (tt * t * wire.End.X),
            (uu * u * wire.Start.Y) +
            (3 * uu * t * wire.Control1.Y) +
            (3 * u * tt * wire.Control2.Y) +
            (tt * t * wire.End.Y));
    }

    private static double DistanceToSegment(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared < 0.0001)
        {
            return Distance(point, start);
        }

        var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);

        return Distance(point, new Point(start.X + (dx * t), start.Y + (dy * t)));
    }

    private static double Distance(Point left, Point right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private readonly record struct WireGeometry(
        Point Start,
        Point Control1,
        Point Control2,
        Point End,
        double ArrowInset);

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
}
