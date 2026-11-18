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
            if (!TryGetModuleWire(modules, edge, out var wire))
            {
                continue;
            }

            var key = (edge.FromModule, edge.ToModule);
            var isPath = _pathHighlight.ModuleEdges.Contains(key);
            var dim = IsFilterActive && !isPath && !_filterMatch.ModuleEdges.Contains(key);

            using var opacity = dim
                ? context.PushOpacity(DimOpacity)
                : default(DrawingContext.PushedState);

            DrawWire(
                context,
                wire,
                isPath ? GraphTheme.PathGlowPen : GraphTheme.ModuleWireGlowPen,
                isPath ? GraphTheme.PathPen : GraphTheme.ModuleWirePen,
                isPath ? GraphTheme.PathColor : GraphTheme.Wire);
        }
    }

    private void DrawChapterWires(DrawingContext context, GraphLayout layout)
    {
        var chaptersById = layout.Chapters.ToDictionary(
            static chapter => chapter.Chapter.ChapterId,
            StringComparer.Ordinal);

        foreach (var edgeLayout in layout.ChapterEdges)
        {
            if (!TryGetChapterWire(layout, chaptersById, edgeLayout, out var wire))
            {
                continue;
            }

            var key = (edgeLayout.Edge.FromChapter, edgeLayout.Edge.ToChapter);
            var isPath = _pathHighlight.ChapterEdges.Contains(key);
            var dim = IsFilterActive && !isPath && !_filterMatch.ChapterEdges.Contains(key);

            using var opacity = dim
                ? context.PushOpacity(DimOpacity)
                : default(DrawingContext.PushedState);

            var isBackEdge = wire.End.X < wire.Start.X;

            IBrush arrow = isPath
                ? GraphTheme.PathColor
                : isBackEdge ? GraphTheme.Warning : GraphTheme.ChapterWire;
            IPen glow = isPath
                ? GraphTheme.PathGlowPen
                : isBackEdge ? GraphTheme.BackEdgePen : GraphTheme.ChapterWireGlowPen;
            IPen core = isPath
                ? GraphTheme.PathPen
                : isBackEdge ? GraphTheme.BackEdgePen : GraphTheme.ChapterWirePen;

            DrawWire(context, wire, glow, core, arrow);
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
            13,
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
            var moduleId = module.Node.ModuleId;
            var isPath = _pathHighlight.Modules.Contains(moduleId);
            var dim = IsFilterActive && !isPath && !_filterMatch.Modules.Contains(moduleId);

            using var opacity = dim
                ? context.PushOpacity(DimOpacity)
                : default(DrawingContext.PushedState);

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
            13,
            GraphTheme.ModuleTitle,
            bold: true);

        DrawModuleBadges(context, node, bounds);

        var isSelected = string.Equals(_selectedModuleId, node.ModuleId, StringComparison.Ordinal);
        var isHovered = string.Equals(_hoveredModuleId, node.ModuleId, StringComparison.Ordinal);
        var isPath = _pathHighlight.Modules.Contains(node.ModuleId);

        if (!isSelected && !isHovered && !isPath)
        {
            return;
        }

        context.DrawRectangle(
            null,
            isSelected
                ? GraphTheme.SelectedPen
                : isHovered ? GraphTheme.HoverPen : GraphTheme.PathModulePen,
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

    private void DrawModuleBadges(
        DrawingContext context,
        GraphModuleNode node,
        Rect bounds)
    {
        var badgeX = bounds.Right - 11;
        var badgeY = bounds.Y + 10;

        if (node.Flags.HasFlag(GraphModuleFlags.Unreachable))
        {
            DrawBadge(context, new Point(badgeX, badgeY), GraphTheme.Danger);
            badgeX -= 17;
        }

        if (node.Flags.HasFlag(GraphModuleFlags.CannotReachEnd))
        {
            DrawBadge(context, new Point(badgeX, badgeY), GraphTheme.Warning);
        }
    }

    private void DrawBadge(
        DrawingContext context,
        Point center,
        IBrush fill)
    {
        context.DrawEllipse(fill, null, center, 7, 7);

        var text = GetFormattedText("!", 9, GraphTheme.BadgeText, bold: true);
        context.DrawText(
            text,
            new Point(
                center.X - (text.Width / 2),
                center.Y - (text.Height / 2) - 0.5));
    }

    private void DrawText(
        DrawingContext context,
        string text,
        Point origin,
        double size,
        IBrush brush,
        bool bold = false)
    {
        context.DrawText(GetFormattedText(text, size, brush, bold), origin);
    }

    private void DrawCenteredText(
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

    private FormattedText GetFormattedText(
        string text,
        double size,
        IBrush brush,
        bool bold)
    {
        var key = (text, size, bold, brush);

        if (_textCache.TryGetValue(key, out var cached))
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

        _textCache[key] = formatted;
        return formatted;
    }

    private static void DrawWire(
        DrawingContext context,
        WireGeometry wire,
        IPen glowPen,
        IPen corePen,
        IBrush arrowBrush)
        => DrawWire(
            context,
            wire.Start,
            wire.Control1,
            wire.Control2,
            wire.End,
            glowPen,
            corePen,
            arrowBrush,
            wire.ArrowInset);

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
