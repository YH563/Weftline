using Weftline.Core;

namespace Weftline.Core.Graph.Analysis;

/// <summary>
/// 选中模块或章节时的高亮结果：相关模块、模块边和章节边。
/// Highlighted modules, module edges, and chapter edges related to the
/// selected module or chapter.
/// </summary>
public sealed record GraphPathHighlight(
    IReadOnlySet<string> Modules,
    IReadOnlySet<(string From, string To)> ModuleEdges,
    IReadOnlySet<(string FromChapter, string ToChapter)> ChapterEdges)
{
    public static GraphPathHighlight Empty { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<(string From, string To)>(),
        new HashSet<(string FromChapter, string ToChapter)>());
}

/// <summary>
/// 计算与选中模块/章节直接相连的高亮：
/// 模块只高亮自身和直接入边/出边；章节高亮章节内模块、内部边和跨章节出入边。
/// Direct highlights for a selected module or chapter.
/// </summary>
public static class GraphPathAnalyzer
{
    public const string EndModuleId = "END";

    public static GraphPathHighlight Build(
        GraphProjection? projection,
        string? selectedModuleId)
        => Build(projection, selectedModuleId, null);

    public static GraphPathHighlight Build(
        GraphProjection? projection,
        string? selectedModuleId,
        string? selectedChapterId)
    {
        if (projection is null)
        {
            return GraphPathHighlight.Empty;
        }

        if (!string.IsNullOrEmpty(selectedChapterId))
        {
            return BuildForChapter(projection, selectedChapterId);
        }

        if (string.IsNullOrEmpty(selectedModuleId))
        {
            return GraphPathHighlight.Empty;
        }

        return BuildForModule(projection, selectedModuleId);
    }

    private static GraphPathHighlight BuildForModule(
        GraphProjection projection,
        string selectedModuleId)
    {
        var modules = new HashSet<string>(StringComparer.Ordinal)
        {
            selectedModuleId,
        };

        var moduleEdges = new HashSet<(string From, string To)>();

        foreach (var edge in projection.ModuleEdges)
        {
            if (string.Equals(edge.FromModule, selectedModuleId, StringComparison.Ordinal) ||
                string.Equals(edge.ToModule, selectedModuleId, StringComparison.Ordinal))
            {
                moduleEdges.Add((edge.FromModule, edge.ToModule));
            }
        }

        var chapterEdges = new HashSet<(string FromChapter, string ToChapter)>();

        foreach (var chapterEdge in projection.ChapterEdges)
        {
            if (chapterEdge.Details.Any(detail =>
                    moduleEdges.Contains((detail.FromModule, detail.ToModule))))
            {
                chapterEdges.Add((chapterEdge.FromChapter, chapterEdge.ToChapter));
            }
        }

        return new GraphPathHighlight(modules, moduleEdges, chapterEdges);
    }

    private static GraphPathHighlight BuildForChapter(
        GraphProjection projection,
        string selectedChapterId)
    {
        var chapter = projection.Chapters.FirstOrDefault(chapter =>
            string.Equals(chapter.ChapterId, selectedChapterId, StringComparison.Ordinal));

        if (chapter is null)
        {
            return GraphPathHighlight.Empty;
        }

        var chapterModules = chapter.ModuleIds.ToHashSet(StringComparer.Ordinal);
        var moduleEdges = new HashSet<(string From, string To)>();
        var chapterEdges = new HashSet<(string FromChapter, string ToChapter)>();

        foreach (var edge in projection.ModuleEdges)
        {
            if (chapterModules.Contains(edge.FromModule) &&
                chapterModules.Contains(edge.ToModule))
            {
                moduleEdges.Add((edge.FromModule, edge.ToModule));
            }
        }

        foreach (var chapterEdge in projection.ChapterEdges)
        {
            var touchesChapter =
                string.Equals(chapterEdge.FromChapter, selectedChapterId, StringComparison.Ordinal) ||
                string.Equals(chapterEdge.ToChapter, selectedChapterId, StringComparison.Ordinal);

            if (!touchesChapter)
            {
                continue;
            }

            chapterEdges.Add((chapterEdge.FromChapter, chapterEdge.ToChapter));

            foreach (var detail in chapterEdge.Details)
            {
                moduleEdges.Add((detail.FromModule, detail.ToModule));
            }
        }

        return new GraphPathHighlight(chapterModules, moduleEdges, chapterEdges);
    }
}
