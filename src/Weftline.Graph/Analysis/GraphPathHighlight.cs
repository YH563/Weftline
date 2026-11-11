using Weftline.Models;

namespace Weftline.Graph.Analysis;

/// <summary>
/// 选中模块时的高亮路径：入口 → 模块、模块 → END。
/// Highlighted paths for a selected module: entry-to-module and module-to-END.
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
/// 根据模块边计算最短路径高亮；BFS 天然避免环。
/// Computes shortest-path highlights from module edges; BFS handles cycles.
/// </summary>
public static class GraphPathAnalyzer
{
    public const string EndModuleId = "END";

    public static GraphPathHighlight Build(
        GraphProjection? projection,
        string? selectedModuleId)
    {
        if (projection is null || string.IsNullOrEmpty(selectedModuleId))
        {
            return GraphPathHighlight.Empty;
        }

        var modules = new HashSet<string>(StringComparer.Ordinal) { selectedModuleId };
        var moduleEdges = new HashSet<(string From, string To)>();
        var adjacency = BuildAdjacency(projection.ModuleEdges);

        if (projection.EntryModuleId is { } entry)
        {
            AddPath(
                FindShortestPath(adjacency, entry, selectedModuleId),
                modules,
                moduleEdges);
        }

        AddPath(
            FindShortestPath(adjacency, selectedModuleId, EndModuleId),
            modules,
            moduleEdges);

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

    private static Dictionary<string, List<string>> BuildAdjacency(
        IReadOnlyList<GraphModuleEdge> edges)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.FromModule, out var targets))
            {
                targets = [];
                adjacency[edge.FromModule] = targets;
            }

            if (!targets.Contains(edge.ToModule, StringComparer.Ordinal))
            {
                targets.Add(edge.ToModule);
            }
        }

        return adjacency;
    }

    private static void AddPath(
        List<string>? path,
        HashSet<string> modules,
        HashSet<(string From, string To)> edges)
    {
        if (path is null)
        {
            return;
        }

        for (var index = 0; index < path.Count; index++)
        {
            if (!string.Equals(path[index], EndModuleId, StringComparison.Ordinal))
            {
                modules.Add(path[index]);
            }

            if (index + 1 < path.Count)
            {
                edges.Add((path[index], path[index + 1]));
            }
        }
    }

    private static List<string>? FindShortestPath(
        IReadOnlyDictionary<string, List<string>> adjacency,
        string start,
        string target)
    {
        if (string.Equals(start, target, StringComparison.Ordinal))
        {
            return [start];
        }

        var previous = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [start] = null,
        };
        var queue = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!adjacency.TryGetValue(current, out var targets))
            {
                continue;
            }

            foreach (var next in targets)
            {
                if (previous.ContainsKey(next))
                {
                    continue;
                }

                previous[next] = current;

                if (string.Equals(next, target, StringComparison.Ordinal))
                {
                    return Reconstruct(previous, next);
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static List<string> Reconstruct(
        IReadOnlyDictionary<string, string?> previous,
        string target)
    {
        var path = new List<string>();
        string? step = target;

        while (step is not null)
        {
            path.Add(step);
            step = previous[step];
        }

        path.Reverse();
        return path;
    }
}
