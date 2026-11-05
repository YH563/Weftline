using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Weftline.Graph.Rendering;
using Weftline.Language.Ir;

namespace Weftline.Graph.Layout;

/// <summary>
/// 根据 GraphProjection 计算剧情图坐标。
///
/// 默认布局：章节横向、模块按 rank 分层；同一 rank 的模块在章节内横向排列，
/// 不同 rank 从上往下排列。章节宽度由本章最宽的一层决定。
///
/// Computes story-graph coordinates from a GraphProjection.
///
/// Default layout: chapters horizontally, modules layered by rank; modules with
/// the same rank are placed side by side, and ranks flow top to bottom. Chapter
/// width is determined by the widest rank row.
/// </summary>
public static class GraphLayoutEngine
{
    public const double CanvasPadding = 28;
    public const double ChapterGap = 72;
    public const double ChapterHeader = 46;
    public const double ChapterPadding = 20;
    public const double ModuleHeight = 36;
    public const double ModuleGap = 26;
    public const double ModuleHorizontalGap = 24;
    public const double RowHeight = ModuleHeight + ModuleGap;
    public const double EndWidth = 110;
    public const double EndHeight = 44;
    public const double MinChapterWidth = 140;
    public const double MaxModuleWidth = 220;

    private static readonly Dictionary<string, double> ModuleWidthCache = new(StringComparer.Ordinal);

    /// <summary>
    /// 构建完整布局。模块宽度按标题测量，章节按 rank 分层。
    /// Builds the full layout. Module widths are measured from titles and chapters are ranked.
    /// </summary>
    public static GraphLayout Build(
        GraphProjection projection,
        GraphLayoutOptions? options = null)
    {
        var modulesById = projection.Modules.ToDictionary(
            static module => module.ModuleId,
            StringComparer.Ordinal);

        var ranks = AssignRanks(projection, modulesById);
        var boundary = BuildBoundaryModules(projection, modulesById);
        var moduleWidths = MeasureModuleWidths(projection.Modules);

        var rowIndexes = ranks.Values
            .Distinct()
            .OrderBy(static rank => rank)
            .Select(static (rank, index) => (rank, index))
            .ToDictionary(
                static pair => pair.rank,
                static pair => pair.index);

        var chapterBuilders = new List<ChapterBuilder>();
        var moduleLayouts = new List<GraphModuleLayout>();
        var currentX = CanvasPadding;

        for (var chapterIndex = 0; chapterIndex < projection.Chapters.Count; chapterIndex++)
        {
            var chapter = projection.Chapters[chapterIndex];

            var order = chapter.ModuleIds
                .Select(static (moduleId, index) => (moduleId, index))
                .ToDictionary(
                    static pair => pair.moduleId,
                    static pair => pair.index,
                    StringComparer.Ordinal);

            var chapterModules = chapter.ModuleIds
                .Where(modulesById.ContainsKey)
                .Select(moduleId => modulesById[moduleId])
                .OrderBy(module => ranks[module.ModuleId])
                .ThenBy(module => order[module.ModuleId])
                .ToList();

            var rows = chapterModules
                .GroupBy(module => ranks[module.ModuleId])
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var modules = group
                        .OrderBy(module => order[module.ModuleId])
                        .ToList();

                    return new ModuleRow(
                        group.Key,
                        modules,
                        modules.Select(module => moduleWidths[module.ModuleId]).ToList());
                })
                .ToList();

            var contentWidth = rows.Count == 0
                ? 0
                : rows.Max(static row => row.Width);

            var chapterWidth = Math.Max(MinChapterWidth, contentWidth + (2 * ChapterPadding));

            var minRow = rows.Count == 0
                ? 0
                : rowIndexes[rows.Min(static row => row.Rank)];

            var maxRow = rows.Count == 0
                ? 0
                : rowIndexes[rows.Max(static row => row.Rank)];

            var chapterTop = CanvasPadding + minRow * RowHeight;
            var chapterHeight = rows.Count == 0
                ? ChapterHeader + (2 * ChapterPadding)
                : ChapterHeader +
                  ChapterPadding +
                  (maxRow - minRow) * RowHeight +
                  ModuleHeight +
                  ChapterPadding;

            var bounds = new Rect(currentX, chapterTop, chapterWidth, chapterHeight);
            var chapterModuleLayouts = new List<GraphModuleLayout>();

            foreach (var row in rows)
            {
                var rowIndex = rowIndexes[row.Rank];
                var moduleY = CanvasPadding +
                    ChapterHeader +
                    ChapterPadding +
                    rowIndex * RowHeight;

                var moduleX = currentX + ((chapterWidth - row.Width) / 2);

                for (var index = 0; index < row.Modules.Count; index++)
                {
                    var module = row.Modules[index];
                    var moduleWidth = row.Widths[index];
                    var moduleBounds = new Rect(moduleX, moduleY, moduleWidth, ModuleHeight);

                    if (options?.ModuleBounds is { } overrides &&
                        overrides.TryGetValue(module.ModuleId, out var manualBounds))
                    {
                        moduleBounds = manualBounds;
                    }

                    var moduleLayout = new GraphModuleLayout(module, moduleBounds);

                    moduleLayouts.Add(moduleLayout);
                    chapterModuleLayouts.Add(moduleLayout);
                    moduleX += moduleWidth + ModuleHorizontalGap;
                }
            }

            if (options?.ChapterBounds is { } chapterOverrides &&
                chapterOverrides.TryGetValue(chapter.ChapterId, out var manualChapterBounds))
            {
                bounds = manualChapterBounds;
            }

            chapterBuilders.Add(new ChapterBuilder(chapter, bounds, chapterModuleLayouts));

            currentX += chapterWidth + ChapterGap;
        }

        var chapterLayouts = chapterBuilders
            .Select(builder => BuildChapterLayout(builder, boundary))
            .ToList();

        var endBounds = options?.EndBounds ?? BuildEndBounds(chapterLayouts);

        var chapterEdges = BuildChapterEdges(projection, chapterLayouts, endBounds);

        var moduleEdges = projection.ModuleEdges
            .Where(static edge => !edge.External && edge.ToModule != "END")
            .GroupBy(static edge => (edge.FromModule, edge.ToModule))
            .Select(static group => group.First())
            .ToList();

        return new GraphLayout(
            chapterLayouts,
            moduleLayouts,
            moduleEdges,
            chapterEdges,
            boundary.EntryModules,
            boundary.ExitModules,
            endBounds,
            new Size(
                Math.Max(
                    endBounds.Right,
                    chapterLayouts.Count == 0 ? 0 : chapterLayouts.Max(static layout => layout.Bounds.Right)) +
                CanvasPadding,
                Math.Max(
                    endBounds.Bottom,
                    chapterLayouts.Count == 0 ? 0 : chapterLayouts.Max(static layout => layout.Bounds.Bottom)) +
                CanvasPadding));
    }

    /// <summary>
    /// 在基础布局上应用模块/章节/END 的手工覆盖，避免拖动时完整重建。
    /// Applies manual module/chapter/END overrides to a base layout without a full rebuild.
    /// </summary>
    public static GraphLayout ApplyOverrides(
        GraphProjection projection,
        GraphLayout baseLayout,
        GraphLayoutOptions? options)
    {
        var hasModuleOverrides = options?.ModuleBounds is { Count: > 0 };
        var hasChapterOverrides = options?.ChapterBounds is { Count: > 0 };
        var hasEndOverride = options?.EndBounds is not null;

        if (!hasModuleOverrides && !hasChapterOverrides && !hasEndOverride)
        {
            return baseLayout;
        }

        var moduleLayouts = baseLayout.Modules
            .Select(module =>
            {
                if (hasModuleOverrides &&
                    options!.ModuleBounds!.TryGetValue(module.Node.ModuleId, out var manualBounds))
                {
                    return new GraphModuleLayout(module.Node, manualBounds);
                }

                return module;
            })
            .ToList();

        var modulesByChapter = moduleLayouts
            .GroupBy(module => module.Node.ChapterId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<GraphModuleLayout>)group.ToList(),
                StringComparer.Ordinal);

        var boundary = new BoundaryModules(
            baseLayout.ChapterEntryModules,
            baseLayout.ChapterExitModules);

        var chapterLayouts = new List<GraphChapterLayout>();

        foreach (var baseChapter in baseLayout.Chapters)
        {
            var chapterId = baseChapter.Chapter.ChapterId;

            var bounds = hasChapterOverrides &&
                options!.ChapterBounds!.TryGetValue(chapterId, out var manualChapterBounds)
                    ? manualChapterBounds
                    : baseChapter.Bounds;

            var modules = modulesByChapter.TryGetValue(chapterId, out var chapterModules)
                ? chapterModules
                : [];

            chapterLayouts.Add(BuildChapterLayout(
                new ChapterBuilder(baseChapter.Chapter, bounds, modules),
                boundary));
        }

        var endBounds = options?.EndBounds ?? BuildEndBounds(chapterLayouts);
        var chapterEdges = BuildChapterEdges(projection, chapterLayouts, endBounds);

        return new GraphLayout(
            chapterLayouts,
            moduleLayouts,
            baseLayout.ModuleEdges,
            chapterEdges,
            boundary.EntryModules,
            boundary.ExitModules,
            endBounds,
            new Size(
                Math.Max(
                    endBounds.Right,
                    chapterLayouts.Count == 0 ? 0 : chapterLayouts.Max(static layout => layout.Bounds.Right)) +
                CanvasPadding,
                Math.Max(
                    endBounds.Bottom,
                    chapterLayouts.Count == 0 ? 0 : chapterLayouts.Max(static layout => layout.Bounds.Bottom)) +
                CanvasPadding));
    }

    private static Dictionary<string, double> MeasureModuleWidths(
        IReadOnlyList<GraphModuleNode> modules)
    {
        var widths = new Dictionary<string, double>(StringComparer.Ordinal);
        var maxWidth = MaxModuleWidth;

        foreach (var module in modules)
        {
            if (!ModuleWidthCache.TryGetValue(module.ModuleId, out var width))
            {
                var text = new FormattedText(
                    module.ModuleId,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    GraphTheme.BoldTypeface,
                    12,
                    Brushes.Black);

                width = Math.Clamp(text.Width + 44, 96, maxWidth);
                ModuleWidthCache[module.ModuleId] = width;
            }

            widths[module.ModuleId] = width;
        }

        return widths;
    }

    private static GraphChapterLayout BuildChapterLayout(
        ChapterBuilder builder,
        BoundaryModules boundary)
    {
        var entryPoints = new List<Point>();
        var exitPoints = new List<Point>();

        foreach (var module in builder.Modules)
        {
            var centerY = module.Bounds.Y + (module.Bounds.Height / 2);

            if (boundary.EntryModules.Contains(module.Node.ModuleId))
            {
                entryPoints.Add(new Point(module.Bounds.X, centerY));
            }

            if (boundary.ExitModules.Contains(module.Node.ModuleId))
            {
                exitPoints.Add(new Point(module.Bounds.Right, centerY));
            }
        }

        var chapterCenterY = builder.Bounds.Y + (builder.Bounds.Height / 2);

        var entryAnchor = new Point(builder.Bounds.X, chapterCenterY);
        var exitAnchor = new Point(builder.Bounds.Right, chapterCenterY);

        return new GraphChapterLayout(
            builder.Chapter,
            builder.Bounds,
            entryAnchor,
            exitAnchor,
            entryPoints
                .Select(point => BuildEntryLink(builder.Bounds, entryAnchor, point))
                .ToList(),
            exitPoints
                .Select(point => BuildExitLink(builder.Bounds, point, exitAnchor))
                .ToList());
    }

    private static GraphBoundaryLinkLayout BuildEntryLink(
        Rect chapterBounds,
        Point anchor,
        Point moduleLeft)
    {
        const double sideInset = 42;

        return new GraphBoundaryLinkLayout(
            anchor,
            new Point(chapterBounds.X + sideInset, anchor.Y),
            new Point(moduleLeft.X - sideInset, moduleLeft.Y),
            moduleLeft);
    }

    private static GraphBoundaryLinkLayout BuildExitLink(
        Rect chapterBounds,
        Point moduleRight,
        Point anchor)
    {
        const double sideInset = 42;

        return new GraphBoundaryLinkLayout(
            moduleRight,
            new Point(moduleRight.X + sideInset, moduleRight.Y),
            new Point(chapterBounds.Right - sideInset, anchor.Y),
            anchor);
    }

    private static BoundaryModules BuildBoundaryModules(
        GraphProjection projection,
        IReadOnlyDictionary<string, GraphModuleNode> modulesById)
    {
        var entryModules = new HashSet<string>(StringComparer.Ordinal);
        var exitModules = new HashSet<string>(StringComparer.Ordinal);

        if (projection.EntryModuleId is { } entryModuleId)
        {
            entryModules.Add(entryModuleId);
        }

        var chaptersWithIncoming = new HashSet<string>(StringComparer.Ordinal);

        foreach (var edge in projection.ModuleEdges)
        {
            if (edge.External && edge.ToModule != "END")
            {
                entryModules.Add(edge.ToModule);
            }

            if (edge.External || edge.ToModule == "END")
            {
                exitModules.Add(edge.FromModule);
            }

            if (edge.External &&
                modulesById.TryGetValue(edge.ToModule, out var targetModule))
            {
                chaptersWithIncoming.Add(targetModule.ChapterId);
            }
        }

        foreach (var chapter in projection.Chapters)
        {
            if (chapter.ModuleIds.Count == 0 ||
                chaptersWithIncoming.Contains(chapter.ChapterId))
            {
                continue;
            }

            entryModules.Add(chapter.ModuleIds[0]);
        }

        return new BoundaryModules(entryModules, exitModules);
    }

    private static Dictionary<string, int> AssignRanks(
        GraphProjection projection,
        IReadOnlyDictionary<string, GraphModuleNode> modulesById)
    {
        var ranks = modulesById.Keys.ToDictionary(
            static moduleId => moduleId,
            static _ => -1,
            StringComparer.Ordinal);

        var entry = projection.EntryModuleId;

        if (entry is null || !modulesById.ContainsKey(entry))
        {
            return ranks;
        }

        var adjacency = BuildAdjacency(projection, modulesById);
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var backEdges = new HashSet<(string From, string To)>();
        var postorder = new List<string>();

        void Visit(string moduleId)
        {
            state[moduleId] = 1;

            if (adjacency.TryGetValue(moduleId, out var targets))
            {
                foreach (var target in targets)
                {
                    if (!modulesById.ContainsKey(target))
                    {
                        continue;
                    }

                    if (state.GetValueOrDefault(target) == 1)
                    {
                        backEdges.Add((moduleId, target));
                    }
                    else if (state.GetValueOrDefault(target) == 0)
                    {
                        Visit(target);
                    }
                }
            }

            state[moduleId] = 2;
            postorder.Add(moduleId);
        }

        Visit(entry);

        postorder.Reverse();
        ranks[entry] = 0;

        foreach (var moduleId in postorder)
        {
            if (ranks[moduleId] < 0 ||
                !adjacency.TryGetValue(moduleId, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                if (!modulesById.ContainsKey(target) ||
                    backEdges.Contains((moduleId, target)))
                {
                    continue;
                }

                ranks[target] = Math.Max(ranks[target], ranks[moduleId] + 1);
            }
        }

        var maxReachableRank = ranks.Values
            .Where(static rank => rank >= 0)
            .DefaultIfEmpty(0)
            .Max();

        var nextUnreachableRank = maxReachableRank + 2;

        foreach (var chapter in projection.Chapters)
        {
            foreach (var moduleId in chapter.ModuleIds)
            {
                if (ranks.TryGetValue(moduleId, out var rank) && rank < 0)
                {
                    ranks[moduleId] = nextUnreachableRank++;
                }
            }
        }

        foreach (var module in projection.Modules)
        {
            if (ranks[module.ModuleId] < 0)
            {
                ranks[module.ModuleId] = nextUnreachableRank++;
            }
        }

        return ranks;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildAdjacency(
        GraphProjection projection,
        IReadOnlyDictionary<string, GraphModuleNode> modulesById)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var module in projection.Modules)
        {
            adjacency[module.ModuleId] = [];
        }

        foreach (var edge in projection.ModuleEdges)
        {
            if (edge.ToModule == "END" ||
                !modulesById.ContainsKey(edge.ToModule))
            {
                continue;
            }

            var targets = adjacency[edge.FromModule];

            if (!targets.Contains(edge.ToModule, StringComparer.Ordinal))
            {
                targets.Add(edge.ToModule);
            }
        }

        return adjacency.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);
    }

    private static Rect BuildEndBounds(IReadOnlyList<GraphChapterLayout> chapters)
    {
        var endX = chapters.Count == 0
            ? CanvasPadding
            : chapters.Max(static chapter => chapter.Bounds.Right) + ChapterGap;

        var top = chapters.Count == 0
            ? CanvasPadding
            : chapters.Min(static chapter => chapter.Bounds.Y);

        var bottom = chapters.Count == 0
            ? CanvasPadding + 200
            : chapters.Max(static chapter => chapter.Bounds.Bottom);

        var endY = Math.Max(CanvasPadding, ((top + bottom) / 2) - (EndHeight / 2));

        return new Rect(endX, endY, EndWidth, EndHeight);
    }

    private static IReadOnlyList<GraphChapterEdgeLayout> BuildChapterEdges(
        GraphProjection projection,
        IReadOnlyList<GraphChapterLayout> chapters,
        Rect endBounds)
    {
        var chaptersById = chapters.ToDictionary(
            static chapter => chapter.Chapter.ChapterId,
            StringComparer.Ordinal);

        var edges = new List<GraphChapterEdgeLayout>();

        foreach (var edge in projection.ChapterEdges)
        {
            if (!chaptersById.TryGetValue(edge.FromChapter, out var from))
            {
                continue;
            }

            var start = from.ExitAnchor;

            Point end;

            if (edge.ToChapter == "END")
            {
                end = new Point(
                    endBounds.X,
                    endBounds.Y + (endBounds.Height / 2));
            }
            else if (chaptersById.TryGetValue(edge.ToChapter, out var to))
            {
                end = to.EntryAnchor;
            }
            else
            {
                continue;
            }

            edges.Add(new GraphChapterEdgeLayout(edge, start, end));
        }

        return edges;
    }

    private sealed record BoundaryModules(
        IReadOnlySet<string> EntryModules,
        IReadOnlySet<string> ExitModules);

    private sealed record ModuleRow(
        int Rank,
        IReadOnlyList<GraphModuleNode> Modules,
        IReadOnlyList<double> Widths)
    {
        public double Width =>
            Widths.Sum() + (ModuleHorizontalGap * Math.Max(0, Widths.Count - 1));
    }

    private sealed class ChapterBuilder
    {
        public ChapterBuilder(
            GraphChapter chapter,
            Rect bounds,
            IReadOnlyList<GraphModuleLayout> modules)
        {
            Chapter = chapter;
            Bounds = bounds;
            Modules = modules;
        }

        public GraphChapter Chapter { get; }

        public Rect Bounds { get; }

        public IReadOnlyList<GraphModuleLayout> Modules { get; }
    }
}
