using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// 一个源码文件的语法树。
/// 包含文件级全局变量、模块、模块内容块和出口分支。
/// The syntax tree for one source file.
/// Contains file-level globals, modules, their content blocks, and exit branches.
/// </summary>
public sealed record ScriptFileSyntax(
    GlobalsSyntax? Globals,
    IReadOnlyList<ModuleSyntax> Modules,
    TextSpan Span);

/// <summary>
/// 文件顶部的全局布尔变量声明。
/// Global boolean variable declarations at the top of a file.
/// </summary>
public sealed record GlobalsSyntax(
    IReadOnlyList<GlobalVariableSyntax> Variables,
    TextSpan Span);

/// <summary>
/// 一条全局布尔变量声明。
/// A global boolean variable declaration.
/// </summary>
public sealed record GlobalVariableSyntax(
    string Name,
    bool Value,
    TextSpan Span);

/// <summary>
/// 一个模块。目前以标题作为模块 ID。
/// Exit 可能为 null：存在语法错误时，解析器仍返回可用的部分语法树，
/// 让编辑器可以继续工作。
/// A module. Its ID is currently the title.
/// Exit may be null: on syntax errors the parser still returns a usable partial
/// tree so the editor can continue working.
/// </summary>
public sealed record ModuleSyntax(
    string Title,
    TextSpan TitleSpan,
    IReadOnlyList<CharacterSyntax> Characters,
    IReadOnlyList<ContentSyntax> Content,
    ExitSyntax? Exit,
    TextSpan Span);

/// <summary>
/// 在模块中声明的角色。
/// A character declared in a module.
/// </summary>
public sealed record CharacterSyntax(
    string Name,
    TextSpan Span);

/// <summary>
/// 一个模块内容块。每个具体块都携带源码范围。
/// A module content block. Every concrete block carries its source span.
/// </summary>
public abstract record ContentSyntax(TextSpan Span);

/// <summary>
/// 旁白：[] 中的纯文本。
/// Narration: plain text inside [].
/// </summary>
public sealed record NarrationSyntax(
    string Text,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>
/// 对话：character:[] 中的纯文本，带有结构化的说话者。
/// Dialogue: plain text inside character:[] with a structured speaker.
/// </summary>
public sealed record DialogueSyntax(
    string Speaker,
    string Text,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>
/// 插图：@fig "path"。
/// Figure: @fig "path".
/// </summary>
public sealed record FigureSyntax(
    string Path,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>
/// 状态更新：@set name true|false。
/// State update: @set name true|false.
/// </summary>
public sealed record SetSyntax(
    string Name,
    bool Value,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>
/// 模块出口块。Character 是块级角色：
/// 有角色表示选择模式；没有角色表示自动模式。
/// A module exit block. Character is the block-level character:
/// a character means choice mode; no character means automatic mode.
/// </summary>
public sealed record ExitSyntax(
    string? Character,
    IReadOnlyList<ExitBranchSyntax> Branches,
    TextSpan Span)
{
    public bool IsChoiceMode => Character is not null;
}

/// <summary>
/// 一条出口分支。Condition 为 null 表示无条件；Label 为 null 表示自动分支。
/// Span 覆盖整条分支；其他范围指向各个字段，用于诊断和文本编辑。
/// One exit branch. A null Condition means unconditional; a null Label means
/// an automatic branch. Span covers the whole branch; the other spans point to
/// individual fields for diagnostics and text edits.
/// </summary>
public sealed record ExitBranchSyntax(
    BoolExprSyntax? Condition,
    string? Label,
    string Target,
    IReadOnlyList<EffectSyntax> Effects,
    TextSpan Span,
    TextSpan TargetSpan,
    TextSpan? ConditionSpan,
    TextSpan? LabelSpan,
    TextSpan? EffectSpan);

/// <summary>
/// 分支效果：@set name true|false。
/// A branch effect: @set name true|false.
/// </summary>
public sealed record EffectSyntax(
    string Name,
    bool Value,
    TextSpan Span);

/// <summary>
/// 布尔条件表达式。
/// A boolean condition expression.
/// </summary>
public abstract record BoolExprSyntax(TextSpan Span);

public sealed record BoolLiteralSyntax(
    bool Value,
    TextSpan Span) : BoolExprSyntax(Span);

public sealed record BoolRefSyntax(
    string Name,
    TextSpan Span) : BoolExprSyntax(Span);

public sealed record NotExprSyntax(
    BoolExprSyntax Operand,
    TextSpan Span) : BoolExprSyntax(Span);

public sealed record AndExprSyntax(
    BoolExprSyntax Left,
    BoolExprSyntax Right,
    TextSpan Span) : BoolExprSyntax(Span);

public sealed record OrExprSyntax(
    BoolExprSyntax Left,
    BoolExprSyntax Right,
    TextSpan Span) : BoolExprSyntax(Span);
