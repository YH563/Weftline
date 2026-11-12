using Weftline.Models;

namespace Weftline.Player;

/// <summary>
/// 播放器正文块的种类。
/// </summary>
public enum RuntimeContentKind
{
    /// <summary>旁白。</summary>
    Narration,

    /// <summary>对话。</summary>
    Dialogue,

    /// <summary>图片。</summary>
    Figure,
}

/// <summary>
/// 正文片段种类：纯文本或 {名字} 引用。
/// </summary>
public enum RuntimeTextSegmentKind
{
    /// <summary>纯文本。</summary>
    Text,

    /// <summary>角色/道具引用。</summary>
    Reference,
}

/// <summary>
/// 播放器载荷中的章节标题。
/// </summary>
public sealed record RuntimeChapter(string Id, string Title);

/// <summary>
/// 播放器载荷中的全局布尔变量及其初始值。
/// </summary>
public sealed record RuntimeGlobal(string Name, bool Initial);

/// <summary>
/// 一个正文片段；Reference 时 Text 是引用名。
/// </summary>
public sealed record RuntimeTextSegment(RuntimeTextSegmentKind Kind, string Text);

/// <summary>
/// 一个正文块：旁白、对话或图片。
/// </summary>
public sealed record RuntimeContent(
    RuntimeContentKind Kind,
    IReadOnlyList<RuntimeTextSegment> Segments,
    string? Speaker = null,
    string? Path = null);

/// <summary>
/// 分支效果：把布尔变量设为指定值。
/// </summary>
public sealed record RuntimeEffect(string Name, bool Value);

/// <summary>
/// 一条出口分支。
/// </summary>
public sealed record RuntimeBranch(
    IrBoolExpr? Condition,
    string? Label,
    string Target,
    IReadOnlyList<RuntimeEffect> Effects);

/// <summary>
/// 一个出口块：自动模式或选择模式。
/// </summary>
public sealed record RuntimeExit(
    ExitMode Mode,
    string? Character,
    IReadOnlyList<RuntimeBranch> Branches);

/// <summary>
/// 播放器模块：完整正文与可选出口块。
/// </summary>
public sealed record RuntimeModule(
    string Id,
    string ChapterId,
    string? Scene,
    IReadOnlyList<RuntimeContent> Content,
    RuntimeExit? Exit);

/// <summary>
/// 播放器载荷：项目名、章节、全局变量、模块和入口。
/// </summary>
public sealed record RuntimeProjection(
    int SchemaVersion,
    string ProjectName,
    string? Entry,
    string End,
    IReadOnlyList<RuntimeChapter> Chapters,
    IReadOnlyList<RuntimeGlobal> Globals,
    IReadOnlyList<RuntimeModule> Modules);
