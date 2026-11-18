using Avalonia;
using Weftline.App.Features.Graph.Layout;
using Weftline.Core;

namespace Weftline.App.Features.Graph.Controls;

public sealed partial class StoryGraphView
{
    /// <summary>
    /// 清除所有手工拖动/缩放，恢复自动布局。
    /// Clears all manual moves/resizes and restores the automatic layout.
    /// </summary>
    public void ResetLayout()
    {
        var hadModuleHover = _hoveredModuleId is not null;

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
        UpdatePathHighlight();
        InvalidateMeasure();
        InvalidateVisual();

        if (hadModuleHover)
        {
            ModuleHoverChanged?.Invoke(this, new ModuleHoverChangedEventArgs(null));
        }
    }

    /// <summary>
    /// 导出当前手工布局覆盖；未知或已删除的节点会被丢弃。
    /// Exports current manual overrides; stale node ids are dropped.
    /// </summary>
    public GraphLayoutSnapshot ExportLayout()
    {
        var moduleIds = Projection?.Modules
            .Select(static module => module.ModuleId)
            .ToHashSet(StringComparer.Ordinal);
        var chapterIds = Projection?.Chapters
            .Select(static chapter => chapter.ChapterId)
            .ToHashSet(StringComparer.Ordinal);

        return new GraphLayoutSnapshot(
            FilterBounds(_moduleBounds, moduleIds),
            FilterBounds(_chapterBounds, chapterIds),
            _endBoundsOverride,
            Zoom);
    }

    /// <summary>
    /// 应用持久化的手工布局覆盖；非法矩形会被忽略。
    /// Applies persisted manual overrides; invalid rectangles are ignored.
    /// </summary>
    public void ApplyLayout(GraphLayoutSnapshot snapshot)
    {
        _moduleBounds.Clear();
        _chapterBounds.Clear();

        foreach (var (moduleId, bounds) in snapshot.ModuleBounds)
        {
            if (IsUsable(bounds))
            {
                _moduleBounds[moduleId] = bounds;
            }
        }

        foreach (var (chapterId, bounds) in snapshot.ChapterBounds)
        {
            if (IsUsable(bounds))
            {
                _chapterBounds[chapterId] = bounds;
            }
        }

        _endBoundsOverride = snapshot.EndBounds is { } endBounds && IsUsable(endBounds)
            ? endBounds
            : null;

        Zoom = double.IsFinite(snapshot.Zoom) && snapshot.Zoom > 0
            ? snapshot.Zoom
            : 1;

        InvalidateMeasure();
        InvalidateVisual();
    }

    private static Dictionary<string, Rect> FilterBounds(
        IReadOnlyDictionary<string, Rect> source,
        IReadOnlySet<string>? allowedIds)
    {
        var result = new Dictionary<string, Rect>(StringComparer.Ordinal);

        foreach (var (id, bounds) in source)
        {
            if ((allowedIds is null || allowedIds.Contains(id)) && IsUsable(bounds))
            {
                result[id] = bounds;
            }
        }

        return result;
    }

    private static bool IsUsable(Rect bounds)
        => double.IsFinite(bounds.X) &&
           double.IsFinite(bounds.Y) &&
           double.IsFinite(bounds.Width) &&
           double.IsFinite(bounds.Height) &&
           bounds.Width > 0 &&
           bounds.Height > 0;
    private bool IsFilterActive => Filter is { IsEmpty: false };

    private static bool HasSameStructure(
        GraphProjection? left,
        GraphProjection? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (!string.Equals(left.EntryModuleId, right.EntryModuleId, StringComparison.Ordinal) ||
            !left.EntryCandidates.SequenceEqual(right.EntryCandidates) ||
            left.Chapters.Count != right.Chapters.Count ||
            left.Modules.Count != right.Modules.Count ||
            left.ModuleEdges.Count != right.ModuleEdges.Count ||
            left.ChapterEdges.Count != right.ChapterEdges.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Chapters.Count; index++)
        {
            var oldChapter = left.Chapters[index];
            var newChapter = right.Chapters[index];

            if (!string.Equals(oldChapter.ChapterId, newChapter.ChapterId, StringComparison.Ordinal) ||
                !string.Equals(oldChapter.Title, newChapter.Title, StringComparison.Ordinal) ||
                oldChapter.Flags != newChapter.Flags ||
                !oldChapter.ModuleIds.SequenceEqual(newChapter.ModuleIds))
            {
                return false;
            }
        }

        for (var index = 0; index < left.Modules.Count; index++)
        {
            var oldModule = left.Modules[index];
            var newModule = right.Modules[index];

            if (!string.Equals(oldModule.ModuleId, newModule.ModuleId, StringComparison.Ordinal) ||
                !string.Equals(oldModule.ChapterId, newModule.ChapterId, StringComparison.Ordinal) ||
                oldModule.Flags != newModule.Flags)
            {
                return false;
            }
        }

        for (var index = 0; index < left.ModuleEdges.Count; index++)
        {
            var oldEdge = left.ModuleEdges[index];
            var newEdge = right.ModuleEdges[index];

            if (!string.Equals(oldEdge.FromModule, newEdge.FromModule, StringComparison.Ordinal) ||
                !string.Equals(oldEdge.ToModule, newEdge.ToModule, StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (var index = 0; index < left.ChapterEdges.Count; index++)
        {
            var oldEdge = left.ChapterEdges[index];
            var newEdge = right.ChapterEdges[index];

            if (!string.Equals(oldEdge.FromChapter, newEdge.FromChapter, StringComparison.Ordinal) ||
                !string.Equals(oldEdge.ToChapter, newEdge.ToChapter, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
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
}
