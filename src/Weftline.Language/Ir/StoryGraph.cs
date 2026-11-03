namespace Weftline.Language.Ir;

/// <summary>
/// 编译器与图视图、HTML 导出之间的中间表示。
/// The intermediate representation between the compiler, graph view, and HTML export.
/// </summary>
public sealed record StoryGraph(
    int SchemaVersion,
    string End,
    string? Entry,
    IrProjectInfo? Project,
    IReadOnlyList<IrChapter> Chapters,
    IReadOnlyDictionary<string, bool> Globals,
    IReadOnlyList<IrModule> Modules,
    IReadOnlyList<IrChapterEdge> ChapterEdges);

public sealed record IrProjectInfo(
    string Name,
    string? Summary,
    IReadOnlyList<IrDoc> Docs);

public sealed record IrDoc(
    string Title,
    string Text);

public sealed record IrChapter(
    string Title,
    string? Description,
    IReadOnlyList<string> Modules);

public sealed record IrChapterEdge(
    string From,
    string To,
    IReadOnlyList<string> Via);

public sealed record IrModule(
    string Id,
    string Title,
    string? Scene,
    string? Chapter,
    IReadOnlyList<IrContentBlock> Content,
    IReadOnlyList<IrExit> Exits);

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

public sealed record IrExit(
    string? Character,
    IrBoolExpr? Condition,
    string? Label,
    string Target,
    IReadOnlyList<IrAssignment> Effects,
    bool External = false);

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
