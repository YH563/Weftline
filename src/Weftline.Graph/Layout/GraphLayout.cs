using Avalonia;
using Weftline.Models;

namespace Weftline.Graph.Layout;

/// <summary>
/// 一次布局的结果：章节容器、模块节点、模块边、章节聚合边和 END 汇点。
/// The result of one layout pass: chapter containers, module nodes, module
/// edges, aggregated chapter edges, and the END sink.
/// </summary>
public sealed record GraphLayout(
    IReadOnlyList<GraphChapterLayout> Chapters,
    IReadOnlyList<GraphModuleLayout> Modules,
    IReadOnlyList<GraphModuleEdge> ModuleEdges,
    IReadOnlyList<GraphChapterEdgeLayout> ChapterEdges,
    IReadOnlySet<string> ChapterEntryModules,
    IReadOnlySet<string> ChapterExitModules,
    Rect EndBounds,
    Size Size)
{
    public GraphChapterLayout? FindChapter(string chapterId)
        => Chapters.FirstOrDefault(chapter =>
            string.Equals(chapter.Chapter.ChapterId, chapterId, StringComparison.Ordinal));

    public GraphModuleLayout? FindModule(string moduleId)
        => Modules.FirstOrDefault(module =>
            string.Equals(module.Node.ModuleId, moduleId, StringComparison.Ordinal));
}

/// <summary>
/// 章节容器布局：边界、入口/出口锚点和内部连接线。
/// Chapter container layout: bounds, entry/exit anchors, and internal links.
/// </summary>
public sealed record GraphChapterLayout(
    GraphChapter Chapter,
    Rect Bounds,
    Point EntryAnchor,
    Point ExitAnchor,
    IReadOnlyList<GraphBoundaryLinkLayout> EntryLinks,
    IReadOnlyList<GraphBoundaryLinkLayout> ExitLinks);

/// <summary>
/// 一条章节入口/出口连接线，使用三次贝塞尔曲线。
/// A chapter entry/exit link using a cubic Bezier curve.
/// </summary>
public sealed record GraphBoundaryLinkLayout(
    Point Start,
    Point Control1,
    Point Control2,
    Point End);

/// <summary>
/// 模块节点布局。
/// Module node layout.
/// </summary>
public sealed record GraphModuleLayout(
    GraphModuleNode Node,
    Rect Bounds);

/// <summary>
/// 一条章节级聚合边布局。
/// Layout for one aggregated chapter edge.
/// </summary>
public sealed record GraphChapterEdgeLayout(
    ChapterEdge Edge,
    Point Start,
    Point End)
{
    public bool IsBackEdge => End.X < Start.X;
}
