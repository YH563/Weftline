using Weftline.Language.Source;

namespace Weftline.Language.Ir;

/// <summary>
/// 剧情图的压缩投影：章节容器、模块节点、模块边和章节聚合边。
///
/// 它不复制完整正文；Graph 视图按 moduleId 回查 StoryGraph。
/// A compressed projection for the story graph: chapter containers, module
/// nodes, module edges, and aggregated chapter edges.
///
/// It does not copy full content; the graph view looks it up by moduleId.
/// </summary>
public sealed record GraphProjection(
    string? EntryModuleId,
    IReadOnlyList<GraphChapter> Chapters,
    IReadOnlyList<GraphModuleNode> Modules,
    IReadOnlyList<GraphModuleEdge> ModuleEdges,
    IReadOnlyList<ChapterEdge> ChapterEdges);

/// <summary>
/// 章节在投影中的状态：入口、有跨章节入边/出边、有到 END 的边。
/// Chapter projection flags: entry, cross-chapter incoming/outgoing edges, and END edges.
/// </summary>
[Flags]
public enum GraphChapterFlags
{
    None = 0,
    Entry = 1,
    HasIncoming = 2,
    HasOutgoing = 4,
    HasEndEdge = 8,
}

/// <summary>
/// 模块在投影中的状态：入口、指向 END、不可达、无法到达 END。
/// Module projection flags: entry, targets END, unreachable, cannot reach END.
/// </summary>
[Flags]
public enum GraphModuleFlags
{
    None = 0,
    Entry = 1,
    TargetsEnd = 2,
    Unreachable = 4,
    CannotReachEnd = 8,
}

/// <summary>
/// 章节容器：标题、模块列表（有序）和状态标记。
/// A chapter container: title, ordered module IDs, and flags.
/// </summary>
public sealed record GraphChapter(
    string ChapterId,
    string Title,
    IReadOnlyList<string> ModuleIds,
    GraphChapterFlags Flags);

/// <summary>
/// 模块节点：图视图需要的元数据，不包含完整正文。
/// A module node: metadata for the graph view without full content.
/// </summary>
public sealed record GraphModuleNode(
    string ModuleId,
    string ChapterId,
    string? Scene,
    int BranchCount,
    GraphModuleFlags Flags,
    GraphModuleSummary Summary,
    SourceLocation Source);

/// <summary>
/// 模块摘要：正文首行、对话数和选项目数。
/// Module summary: first content line, dialogue count, and choice count.
/// </summary>
public sealed record GraphModuleSummary(
    string? FirstLine,
    int DialogueCount,
    int ChoiceCount);

/// <summary>
/// 一条真实模块边。External 表示目标位于其他章节。
/// A real module edge. External means the target is in another chapter.
/// </summary>
public sealed record GraphModuleEdge(
    string FromModule,
    string ToModule,
    ExitMode Mode,
    string? Label,
    IrBoolExpr? Condition,
    bool External,
    SourceLocation Source);
