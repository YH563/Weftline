using Weftline.Language.Source;

namespace Weftline.Language.Ir;

/// <summary>
/// 从 StoryGraph 派生的分析结果：可达性、引用索引、邻接表和章节级聚合边。
///
/// 这些数据不进入 IR，也不写回 StoryGraph；图视图、筛选和路径高亮共用它们。
///
/// Derived analysis over a StoryGraph: reachability, reference indexes,
/// adjacency, and aggregated chapter edges.
///
/// This data does not enter the IR and is not written back to StoryGraph;
/// graph views, filters, and path highlighting share it.
/// </summary>
public sealed record GraphAnalysis(
    IReadOnlySet<string> Reachable,
    IReadOnlySet<string> CanReachEnd,
    ReferenceIndex References,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Adjacency,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ReverseAdjacency,
    IReadOnlyList<ChapterEdge> ChapterEdges)
{
    /// <summary>
    /// 枚举从 from 到 to 的简单路径，最多返回 limit 条。
    /// to 可以是模块 ID，也可以是 StoryGraph.End（通常是 "END"）。
    /// Enumerates simple paths from from to to, up to limit results.
    /// to may be a module ID or StoryGraph.End (usually "END").
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> FindPaths(
        string from,
        string to,
        int limit = 50)
    {
        if (limit <= 0 || string.IsNullOrEmpty(from))
        {
            return [];
        }

        if (!Adjacency.ContainsKey(from))
        {
            return [];
        }

        var results = new List<IReadOnlyList<string>>();
        var path = new List<string>();
        var onPath = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string current)
        {
            if (results.Count >= limit)
            {
                return;
            }

            path.Add(current);
            onPath.Add(current);

            if (string.Equals(current, to, StringComparison.Ordinal))
            {
                results.Add(path.ToList());
            }
            else if (Adjacency.TryGetValue(current, out var next))
            {
                foreach (var target in next)
                {
                    if (!onPath.Contains(target))
                    {
                        Visit(target);
                    }
                }
            }

            onPath.Remove(current);
            path.RemoveAt(path.Count - 1);
        }

        Visit(from);
        return results;
    }
}

/// <summary>
/// 按符号种类归类的引用索引。值是有序的模块 ID 列表，不重复。
/// Reference indexes grouped by symbol kind. Values are ordered, duplicate-free module IDs.
/// </summary>
public sealed record ReferenceIndex(
    IReadOnlyDictionary<string, IReadOnlyList<string>> Characters,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Items,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Scenes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Variables);

/// <summary>
/// 一条聚合后的章节边。Count 等于底层模块分支条数。
/// An aggregated chapter edge. Count equals the number of underlying module branches.
/// </summary>
public sealed record ChapterEdge(
    string FromChapter,
    string ToChapter,
    IReadOnlyList<ChapterEdgeDetail> Details)
{
    /// <summary>
    /// 底层分支条数。
    /// Number of underlying branches.
    /// </summary>
    public int Count => Details.Count;
}

/// <summary>
/// 聚合边下的一条底层模块分支。
/// One underlying module branch of an aggregated chapter edge.
/// </summary>
public sealed record ChapterEdgeDetail(
    string FromModule,
    string ToModule,
    ExitMode Mode,
    string? Label,
    IrBoolExpr? Condition,
    SourceLocation Source);
