using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// 一个源文件解析后的抽象语法树。
/// Step 1 只解析 globals、模块头、内容块，并把 exit 原文暂存起来。
/// </summary>
public sealed record ScriptFileSyntax(
    GlobalsSyntax? Globals,
    IReadOnlyList<ModuleSyntax> Modules,
    TextSpan Span);

/// <summary>文件顶部的全局布尔变量声明。</summary>
public sealed record GlobalsSyntax(
    IReadOnlyList<GlobalVariableSyntax> Variables,
    TextSpan Span);

public sealed record GlobalVariableSyntax(
    string Name,
    bool Value,
    TextSpan Span);

/// <summary>
/// 一个模块。模块 ID 暂时等于 Title。
/// Exit 在 Step 1 里只保存原文行，Step 2 再解析成真正的分支。
/// </summary>
public sealed record ModuleSyntax(
    string Title,
    TextSpan TitleSpan,
    IReadOnlyList<CharacterSyntax> Characters,
    IReadOnlyList<ContentSyntax> Content,
    ExitSyntax? Exit,
    TextSpan Span);

public sealed record CharacterSyntax(
    string Name,
    TextSpan Span);

/// <summary>模块内容块。所有具体内容块都带自己的源码位置。</summary>
public abstract record ContentSyntax(TextSpan Span);

/// <summary>旁白：[] 中的纯文本。</summary>
public sealed record NarrationSyntax(
    string Text,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>对话：角色:[] 中的纯文本，说话人是结构化的。</summary>
public sealed record DialogueSyntax(
    string Speaker,
    string Text,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>图片：@fig "path"。</summary>
public sealed record FigureSyntax(
    string Path,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>状态修改：@set name true|false。</summary>
public sealed record SetSyntax(
    string Name,
    bool Value,
    TextSpan Span) : ContentSyntax(Span);

/// <summary>
/// Step 1 的出口块占位：保留原文，等 Step 2 解析。
/// RawLines 包含 @next 行本身以及后续分支行。
/// </summary>
public sealed record ExitSyntax(
    TextSpan Span,
    IReadOnlyList<string> RawLines);
