namespace Weftline.Models;

/// <summary>
/// 聚合章节边下的一条底层模块分支。
/// </summary>
public sealed record ChapterEdgeDetail(
    string FromModule,
    string ToModule,
    ExitMode Mode,
    string? Label,
    IrBoolExpr? Condition,
    SourceLocation Source);

/// <summary>
/// 一条聚合后的章节边；Count 等于底层模块分支条数。
/// </summary>
public sealed record ChapterEdge(
    string FromChapter,
    string ToChapter,
    IReadOnlyList<ChapterEdgeDetail> Details)
{
    /// <summary>
    /// 底层模块分支条数。
    /// </summary>
    public int Count => Details.Count;
}
