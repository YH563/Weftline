using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// The syntax tree for one source file.
/// Contains file-level globals, modules, their content blocks, and exit branches.
/// </summary>
public sealed record ScriptFileSyntax(
    GlobalsSyntax? Globals,
    IReadOnlyList<ModuleSyntax> Modules,
    TextSpan Span);

/// <summary>Global boolean variable declarations at the top of a file.</summary>
public sealed record GlobalsSyntax(
    IReadOnlyList<GlobalVariableSyntax> Variables,
    TextSpan Span);

/// <summary>A global boolean variable declaration.</summary>
public sealed record GlobalVariableSyntax(
    string Name,
    bool Value,
    TextSpan Span);

/// <summary>
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

/// <summary>A character declared in a module.</summary>
public sealed record CharacterSyntax(
    string Name,
    TextSpan Span);

/// <summary>A module content block. Every concrete block carries its source span.</summary>
public abstract record ContentSyntax(TextSpan Span);

/// <summary>Narration: plain text inside [].</summary>
public sealed record NarrationSyntax(
    string Text,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>Dialogue: plain text inside character:[] with a structured speaker.</summary>
public sealed record DialogueSyntax(
    string Speaker,
    string Text,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>Figure: @fig "path".</summary>
public sealed record FigureSyntax(
    string Path,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>State update: @set name true|false.</summary>
public sealed record SetSyntax(
    string Name,
    bool Value,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>
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

/// <summary>A branch effect: @set name true|false.</summary>
public sealed record EffectSyntax(
    string Name,
    bool Value,
    TextSpan Span);

/// <summary>A boolean condition expression.</summary>
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
