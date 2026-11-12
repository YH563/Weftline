using Weftline.Models;

namespace Weftline.Native;

/// <summary>
/// 项目编译的 JSON 响应。
/// </summary>
public sealed class NativeProjectResponse
{
    public int SchemaVersion { get; set; }
    public string? Version { get; set; }
    public bool HasErrors { get; set; }
    public string? ProjectName { get; set; }
    public string? Error { get; set; }
    public List<NativeDiagnostic> Diagnostics { get; set; } = [];
    public NativeProjectSymbols? Symbols { get; set; }
    public NativeStoryGraph? Graph { get; set; }
    public NativeProjection? Projection { get; set; }
}

/// <summary>
/// 单文件解析的 JSON 响应。
/// </summary>
public sealed class NativeParseResponse
{
    public int SchemaVersion { get; set; }
    public string? Version { get; set; }
    public string? FilePath { get; set; }
    public long RequestVersion { get; set; }
    public bool HasErrors { get; set; }
    public string? Error { get; set; }
    public List<NativeDiagnostic> Diagnostics { get; set; } = [];
}

/// <summary>
/// 项目级符号与描述。
/// </summary>
public sealed class NativeProjectSymbols
{
    public List<NativeSymbolInfo> Variables { get; set; } = [];
    public List<NativeSymbolInfo> Characters { get; set; } = [];
    public List<NativeSymbolInfo> Items { get; set; } = [];
    public List<NativeSymbolInfo> Scenes { get; set; } = [];
    public List<NativeChapterInfo> Chapters { get; set; } = [];

    /// <summary>
    /// 转换为编辑器使用的符号模型。
    /// </summary>
    public WeftProjectSymbols ToSymbols()
        => new(
            Variables.Select(static symbol => ToSymbol(symbol)).ToList(),
            Characters.Select(static symbol => ToSymbol(symbol)).ToList(),
            Items.Select(static symbol => ToSymbol(symbol)).ToList(),
            Scenes.Select(static symbol => ToSymbol(symbol)).ToList(),
            Chapters.Select(static chapter => new WeftChapterInfo(
                chapter.Id,
                chapter.Title,
                chapter.Description,
                chapter.ModuleIds,
                ToSource(chapter.FilePath, chapter.Start, chapter.Length))).ToList());

    private static WeftSymbolInfo ToSymbol(NativeSymbolInfo symbol)
        => new(
            symbol.Name,
            symbol.Initial,
            symbol.Description,
            ToSource(symbol.FilePath, symbol.Start, symbol.Length));

    private static SourceLocation ToSource(string filePath, int start, int length)
        => new(filePath, new TextSpan(start, length));
}

/// <summary>
/// 一个符号及其定义位置。
/// </summary>
public sealed class NativeSymbolInfo
{
    public string Name { get; set; } = string.Empty;
    public bool? Initial { get; set; }
    public string? Description { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int Start { get; set; }
    public int Length { get; set; }
}

/// <summary>
/// 一个章节及其描述。
/// </summary>
public sealed class NativeChapterInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> ModuleIds { get; set; } = [];
    public string FilePath { get; set; } = string.Empty;
    public int Start { get; set; }
    public int Length { get; set; }
}

/// <summary>
/// 一条带源码位置的诊断。
/// </summary>
public sealed class NativeDiagnostic
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Start { get; set; }
    public int Length { get; set; }
    public NativeRelated? Related { get; set; }
}

/// <summary>
/// 诊断的关联位置，例如重复声明或引用来源。
/// </summary>
public sealed class NativeRelated
{
    public string FilePath { get; set; } = string.Empty;
    public int Start { get; set; }
    public int Length { get; set; }
}

/// <summary>
/// C++ StoryGraph 的最小 JSON 投影。
/// </summary>
public sealed class NativeStoryGraph
{
    public List<NativeGlobal> Globals { get; set; } = [];
}

/// <summary>
/// 一个全局变量。
/// </summary>
public sealed class NativeGlobal
{
    public string Name { get; set; } = string.Empty;
    public bool Initial { get; set; }
}

/// <summary>
/// 剧情图压缩投影的 JSON DTO；可映射为 <see cref="Weftline.Models.GraphProjection"/>。
/// </summary>
public sealed class NativeProjection
{
    public string? EntryModuleId { get; set; }
    public List<NativeGraphChapter> Chapters { get; set; } = [];
    public List<NativeGraphModuleNode> Modules { get; set; } = [];
    public List<NativeGraphModuleEdge> ModuleEdges { get; set; } = [];
    public List<NativeChapterEdge> ChapterEdges { get; set; } = [];
    public NativeGraphReferences? References { get; set; }

    /// <summary>
    /// 转换为显示层图投影。
    /// </summary>
    public GraphProjection ToGraphProjection()
        => new(
            EntryModuleId,
            Chapters.Select(static chapter => new GraphChapter(
                chapter.ChapterId,
                chapter.Title,
                chapter.ModuleIds,
                ParseChapterFlags(chapter.Flags))).ToList(),
            Modules.Select(static module => new GraphModuleNode(
                module.ModuleId,
                module.ChapterId,
                module.Scene,
                module.BranchCount,
                ParseModuleFlags(module.Flags),
                new GraphModuleSummary(
                    module.Summary?.FirstLine,
                    module.Summary?.DialogueCount ?? 0,
                    module.Summary?.ChoiceCount ?? 0),
                ToSource(module.Source))).ToList(),
            ModuleEdges.Select(static edge => new GraphModuleEdge(
                edge.FromModule,
                edge.ToModule,
                ParseMode(edge.Mode),
                edge.Label,
                ToBoolExpr(edge.Condition),
                edge.External,
                ToSource(edge.Source))).ToList(),
            ChapterEdges.Select(static edge => new ChapterEdge(
                edge.FromChapter,
                edge.ToChapter,
                edge.Details.Select(static detail => new ChapterEdgeDetail(
                    detail.FromModule,
                    detail.ToModule,
                    ParseMode(detail.Mode),
                    detail.Label,
                    ToBoolExpr(detail.Condition),
                    ToSource(detail.Source))).ToList())).ToList(),
            References?.ToReferenceIndex() ?? GraphReferenceIndex.Empty);

    private static GraphChapterFlags ParseChapterFlags(IEnumerable<string>? flags)
    {
        var result = GraphChapterFlags.None;
        foreach (var flag in flags ?? [])
        {
            result |= flag switch
            {
                "entry" => GraphChapterFlags.Entry,
                "hasIncoming" => GraphChapterFlags.HasIncoming,
                "hasOutgoing" => GraphChapterFlags.HasOutgoing,
                "hasEndEdge" => GraphChapterFlags.HasEndEdge,
                _ => GraphChapterFlags.None,
            };
        }

        return result;
    }

    private static GraphModuleFlags ParseModuleFlags(IEnumerable<string>? flags)
    {
        var result = GraphModuleFlags.None;
        foreach (var flag in flags ?? [])
        {
            result |= flag switch
            {
                "entry" => GraphModuleFlags.Entry,
                "targetsEnd" => GraphModuleFlags.TargetsEnd,
                "unreachable" => GraphModuleFlags.Unreachable,
                "cannotReachEnd" => GraphModuleFlags.CannotReachEnd,
                _ => GraphModuleFlags.None,
            };
        }

        return result;
    }

    private static ExitMode ParseMode(string? mode)
        => string.Equals(mode, "choice", StringComparison.OrdinalIgnoreCase)
            ? ExitMode.Choice
            : ExitMode.Auto;

    internal static IrBoolExpr? ToBoolExpr(NativeBoolExpr? expression)
    {
        if (expression is null)
        {
            return null;
        }

        return expression.Kind switch
        {
            "literal" => IrBoolExpr.Literal(expression.Value ?? false),
            "ref" => IrBoolExpr.Ref(expression.Name ?? string.Empty),
            "not" => IrBoolExpr.Not(ToBoolExpr(expression.Expr) ?? IrBoolExpr.Literal(true)),
            "and" => IrBoolExpr.And(
                ToBoolExpr(expression.Left) ?? IrBoolExpr.Literal(true),
                ToBoolExpr(expression.Right) ?? IrBoolExpr.Literal(true)),
            "or" => IrBoolExpr.Or(
                ToBoolExpr(expression.Left) ?? IrBoolExpr.Literal(true),
                ToBoolExpr(expression.Right) ?? IrBoolExpr.Literal(true)),
            _ => null,
        };
    }

    private static SourceLocation ToSource(NativeSourceLocation? source)
        => source is null
            ? new SourceLocation(string.Empty, new TextSpan(0, 0))
            : new SourceLocation(
                source.FilePath,
                new TextSpan(source.Span.Start, source.Span.Length));
}

/// <summary>
/// 符号 → 模块引用索引的 JSON DTO。
/// </summary>
public sealed class NativeGraphReferences
{
    public Dictionary<string, List<string>> Characters { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> Items { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> Scenes { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<string>> Variables { get; set; } = new(StringComparer.Ordinal);

    public GraphReferenceIndex ToReferenceIndex()
        => new(
            ToReadOnly(Characters),
            ToReadOnly(Items),
            ToReadOnly(Scenes),
            ToReadOnly(Variables));

    private static Dictionary<string, IReadOnlyList<string>> ToReadOnly(
        Dictionary<string, List<string>> source)
        => source.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);
}

/// <summary>
/// 章节容器的 JSON DTO。
/// </summary>
public sealed class NativeGraphChapter
{
    public string ChapterId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> ModuleIds { get; set; } = [];
    public List<string> Flags { get; set; } = [];
}

/// <summary>
/// 模块节点的 JSON DTO。
/// </summary>
public sealed class NativeGraphModuleNode
{
    public string ModuleId { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public string? Scene { get; set; }
    public int BranchCount { get; set; }
    public List<string> Flags { get; set; } = [];
    public NativeGraphModuleSummary? Summary { get; set; }
    public NativeSourceLocation? Source { get; set; }
}

/// <summary>
/// 模块摘要。
/// </summary>
public sealed class NativeGraphModuleSummary
{
    public string? FirstLine { get; set; }
    public int DialogueCount { get; set; }
    public int ChoiceCount { get; set; }
}

/// <summary>
/// 一条模块边。
/// </summary>
public sealed class NativeGraphModuleEdge
{
    public string FromModule { get; set; } = string.Empty;
    public string ToModule { get; set; } = string.Empty;
    public string Mode { get; set; } = "auto";
    public string? Label { get; set; }
    public NativeBoolExpr? Condition { get; set; }
    public bool External { get; set; }
    public NativeSourceLocation? Source { get; set; }
}

/// <summary>
/// 一条章节聚合边。
/// </summary>
public sealed class NativeChapterEdge
{
    public string FromChapter { get; set; } = string.Empty;
    public string ToChapter { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<NativeChapterEdgeDetail> Details { get; set; } = [];
}

/// <summary>
/// 章节聚合边下的模块分支明细。
/// </summary>
public sealed class NativeChapterEdgeDetail
{
    public string FromModule { get; set; } = string.Empty;
    public string ToModule { get; set; } = string.Empty;
    public string Mode { get; set; } = "auto";
    public string? Label { get; set; }
    public NativeBoolExpr? Condition { get; set; }
    public NativeSourceLocation? Source { get; set; }
}

/// <summary>
/// 扁平布尔表达式。
/// </summary>
public sealed class NativeBoolExpr
{
    public string Kind { get; set; } = "literal";
    public bool? Value { get; set; }
    public string? Name { get; set; }
    public NativeBoolExpr? Expr { get; set; }
    public NativeBoolExpr? Left { get; set; }
    public NativeBoolExpr? Right { get; set; }
}

/// <summary>
/// 带文件路径的源码位置。
/// </summary>
public sealed class NativeSourceLocation
{
    public string FilePath { get; set; } = string.Empty;
    public NativeTextSpan Span { get; set; } = new();
}

/// <summary>
/// UTF-16 code unit 范围。
/// </summary>
public sealed class NativeTextSpan
{
    public int Start { get; set; }
    public int Length { get; set; }
}
