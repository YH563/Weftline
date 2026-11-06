namespace Weftline.Models;

/// <summary>
/// 章节在投影中的状态：入口、有跨章节入边/出边、有到 END 的边。
/// </summary>
[Flags]
public enum GraphChapterFlags
{
    /// <summary>无标记。</summary>
    None = 0,

    /// <summary>包含入口模块。</summary>
    Entry = 1,

    /// <summary>有跨章节入边。</summary>
    HasIncoming = 2,

    /// <summary>有跨章节出边。</summary>
    HasOutgoing = 4,

    /// <summary>有指向 END 的边。</summary>
    HasEndEdge = 8,
}

/// <summary>
/// 模块在投影中的状态：入口、指向 END、不可达、无法到达 END。
/// </summary>
[Flags]
public enum GraphModuleFlags
{
    /// <summary>无标记。</summary>
    None = 0,

    /// <summary>入口模块。</summary>
    Entry = 1,

    /// <summary>存在指向 END 的出口。</summary>
    TargetsEnd = 2,

    /// <summary>从入口不可达。</summary>
    Unreachable = 4,

    /// <summary>无法到达 END。</summary>
    CannotReachEnd = 8,
}

/// <summary>
/// 剧情图的压缩投影：章节容器、模块节点、模块边和章节聚合边。
/// </summary>
public sealed record GraphProjection(
    string? EntryModuleId,
    IReadOnlyList<GraphChapter> Chapters,
    IReadOnlyList<GraphModuleNode> Modules,
    IReadOnlyList<GraphModuleEdge> ModuleEdges,
    IReadOnlyList<ChapterEdge> ChapterEdges);

/// <summary>
/// 章节容器：标题、有序模块 ID 和状态标记。
/// </summary>
public sealed record GraphChapter(
    string ChapterId,
    string Title,
    IReadOnlyList<string> ModuleIds,
    GraphChapterFlags Flags);

/// <summary>
/// 模块节点：图视图需要的元数据，不含完整正文。
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
/// 模块摘要：正文首行、对话数和选项数。
/// </summary>
public sealed record GraphModuleSummary(
    string? FirstLine,
    int DialogueCount,
    int ChoiceCount);

/// <summary>
/// 一条真实模块边；External 表示目标位于其他章节。
/// </summary>
public sealed record GraphModuleEdge(
    string FromModule,
    string ToModule,
    ExitMode Mode,
    string? Label,
    IrBoolExpr? Condition,
    bool External,
    SourceLocation Source);
