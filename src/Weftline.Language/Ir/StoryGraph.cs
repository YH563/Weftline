using Weftline.Language.Source;

namespace Weftline.Language.Ir;

/// <summary>
/// 图与执行语义：有序全局变量、模块、出口块和分支。
/// Graph and execution semantics: ordered globals, modules, exit blocks, and branches.
/// </summary>
public sealed record StoryGraph(
    int SchemaVersion,
    string End,
    string? Entry,
    IReadOnlyList<IrGlobal> Globals,
    IReadOnlyList<IrModule> Modules);

public sealed record IrGlobal(
    string Name,
    bool Initial);

public sealed record IrModule(
    string Id,
    string ChapterId,
    string? Scene,
    IReadOnlyList<IrContentBlock> Content,
    IrExitBlock? Exit,
    SourceLocation Source);

public enum ExitMode
{
    Auto,
    Choice,
}

/// <summary>
/// 一个模块的出口块。模式、角色和分支顺序都是语义的一部分。
/// The exit block of one module. Mode, character, and branch order are semantic.
/// </summary>
public sealed record IrExitBlock(
    ExitMode Mode,
    string? Character,
    IReadOnlyList<IrExitBranch> Branches,
    SourceLocation Source);

public sealed record IrExitBranch(
    IrBoolExpr? Condition,
    string? Label,
    string Target,
    IReadOnlyList<IrAssignment> Effects,
    SourceLocation Source);

public sealed record IrContentBlock(
    string Kind,
    IReadOnlyList<IrTextSegment>? Segments = null,
    string? Speaker = null,
    string? Path = null)
{
    public static IrContentBlock Narration(IReadOnlyList<IrTextSegment> segments)
        => new("narration", segments);

    public static IrContentBlock Dialogue(string speaker, IReadOnlyList<IrTextSegment> segments)
        => new("dialogue", segments, speaker);

    public static IrContentBlock Figure(string path)
        => new("figure", Path: path);
}

public sealed record IrTextSegment(
    string Kind,
    string? Text = null,
    string? Name = null);

public sealed record IrAssignment(
    string Name,
    bool Value);

public sealed record IrBoolExpr(
    string Kind,
    bool? Value = null,
    string? Name = null,
    IrBoolExpr? Expr = null,
    IrBoolExpr? Left = null,
    IrBoolExpr? Right = null)
{
    public static IrBoolExpr Literal(bool value) => new("literal", Value: value);

    public static IrBoolExpr Ref(string name) => new("ref", Name: name);

    public static IrBoolExpr Not(IrBoolExpr expression) => new("not", Expr: expression);

    public static IrBoolExpr And(IrBoolExpr left, IrBoolExpr right) => new("and", Left: left, Right: right);

    public static IrBoolExpr Or(IrBoolExpr left, IrBoolExpr right) => new("or", Left: left, Right: right);
}
