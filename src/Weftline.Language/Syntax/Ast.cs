using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// 一个源码文件的语法树。文件分为模块脚本 `.weft` 和项目总览 `.wstory`。
/// The syntax tree for one source file. Files are either `.weft` module scripts
/// or `.wstory` project overviews.
/// </summary>
public abstract record ScriptFileSyntax(TextSpan Span);

/// <summary>
/// 一个 `.weft` 模块脚本文件。文件顶部按分组声明变量、角色、道具和场景，
/// 后面是若干模块。声明是项目符号的真实来源。
/// A `.weft` module script file. Symbol declarations are grouped at the top,
/// followed by modules. These declarations are the source of truth for symbols.
/// </summary>
public sealed record WeftFileSyntax(
    IReadOnlyList<WeftVariableSyntax> Variables,
    IReadOnlyList<WeftCharacterSyntax> Characters,
    IReadOnlyList<WeftItemSyntax> Items,
    IReadOnlyList<WeftSceneSyntax> Scenes,
    IReadOnlyList<ModuleSyntax> Modules,
    TextSpan Span) : ScriptFileSyntax(Span);

/// <summary>
/// 一条变量声明。变量必须带初始值。
/// A variable declaration. Variables must provide an initial value.
/// </summary>
public sealed record WeftVariableSyntax(
    string Name,
    bool Value,
    TextSpan Span);

/// <summary>
/// 一条角色声明。
/// A character declaration.
/// </summary>
public sealed record WeftCharacterSyntax(
    string Name,
    TextSpan Span);

/// <summary>
/// 一条道具声明。
/// An item declaration.
/// </summary>
public sealed record WeftItemSyntax(
    string Name,
    TextSpan Span);

/// <summary>
/// 一条场景声明。
/// A scene declaration.
/// </summary>
public sealed record WeftSceneSyntax(
    string Name,
    TextSpan Span);

/// <summary>
/// 一个 `.wstory` 项目总览文件。声明由 `.weft` 汇总生成，描述由作者手写。
/// A `.wstory` project overview file. Declarations are generated from `.weft`
/// files; descriptions are authored by hand.
/// </summary>
public sealed record WStoryFileSyntax(
    string ProjectName,
    TextSpan ProjectNameSpan,
    TextBlockSyntax? Summary,
    IReadOnlyList<DocSyntax> Docs,
    IReadOnlyList<WStoryCharacterSyntax> Characters,
    IReadOnlyList<WStoryItemSyntax> Items,
    IReadOnlyList<WStorySceneSyntax> Scenes,
    IReadOnlyList<WStoryVariableSyntax> Variables,
    TextSpan Span) : ScriptFileSyntax(Span);

/// <summary>
/// 一条 @doc 设定文档。
/// A @doc world-setting document.
/// </summary>
public sealed record DocSyntax(
    string Title,
    TextSpan TitleSpan,
    TextBlockSyntax Body,
    TextSpan Span);

/// <summary>
/// 纯文本块，用于描述、概要和设定文档。
/// A plain text block used by descriptions, summaries, and docs.
/// </summary>
public sealed record TextBlockSyntax(
    string Text,
    TextSpan Span);

public sealed record WStoryVariableSyntax(
    string Name,
    bool Value,
    TextBlockSyntax? Description,
    TextSpan Span);

public sealed record WStoryCharacterSyntax(
    string Name,
    TextBlockSyntax? Description,
    TextSpan Span);

public sealed record WStoryItemSyntax(
    string Name,
    TextBlockSyntax? Description,
    TextSpan Span);

public sealed record WStorySceneSyntax(
    string Name,
    TextBlockSyntax? Description,
    TextSpan Span);

/// <summary>
/// 一个模块。目前以标题作为模块 ID。模块是图的唯一节点。
/// Exit 可能为 null：存在语法错误时，解析器仍返回可用的部分语法树，
/// 让编辑器可以继续工作。
/// A module. Its ID is currently the title. A module is the only graph node.
/// Exit may be null: on syntax errors the parser still returns a usable partial
/// tree so the editor can continue working.
/// </summary>
public sealed record ModuleSyntax(
    string Title,
    TextSpan TitleSpan,
    string? Scene,
    TextSpan? SceneSpan,
    IReadOnlyList<ContentSyntax> Content,
    ExitSyntax? Exit,
    TextSpan Span);

/// <summary>
/// 一个模块内容块。每个具体块都携带源码范围。
/// A module content block. Every concrete block carries its source span.
/// </summary>
public abstract record ContentSyntax(TextSpan Span);

/// <summary>
/// 正文文本段。正文由字面量片段和 {名字} 引用片段组成。
/// A text segment. Text is made of literal segments and {name} reference segments.
/// </summary>
public abstract record TextSegmentSyntax(TextSpan Span)
{
    public static string GetText(IReadOnlyList<TextSegmentSyntax> segments)
        => string.Concat(segments.Select(
            static segment => segment switch
            {
                TextLiteralSyntax literal => literal.Text,
                TextReferenceSyntax reference => reference.Name,
                _ => string.Empty,
            }));
}

/// <summary>
/// 正文中的纯文本片段。
/// A literal text segment.
/// </summary>
public sealed record TextLiteralSyntax(
    string Text,
    TextSpan Span) : TextSegmentSyntax(Span);

/// <summary>
/// 正文中的 {名字} 引用，只允许引用角色或道具。
/// A {name} reference that may only refer to a character or an item.
/// </summary>
public sealed record TextReferenceSyntax(
    string Name,
    TextSpan Span) : TextSegmentSyntax(Span);

/// <summary>
/// 旁白：[] 中的文本。
/// Narration: text inside [].
/// </summary>
public sealed record NarrationSyntax(
    IReadOnlyList<TextSegmentSyntax> Segments,
    TextSpan Span) : ContentSyntax(Span)
{
    public string Text => TextSegmentSyntax.GetText(Segments);
}

/// <summary>
/// 对话：character:[] 中的文本，带有结构化的说话者。
/// Dialogue: text inside character:[] with a structured speaker.
/// </summary>
public sealed record DialogueSyntax(
    string Speaker,
    IReadOnlyList<TextSegmentSyntax> Segments,
    TextSpan Span) : ContentSyntax(Span)
{
    public string Text => TextSegmentSyntax.GetText(Segments);
}

/// <summary>
/// 插图：@fig "path"。
/// Figure: @fig "path".
/// </summary>
public sealed record FigureSyntax(
    string Path,
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
