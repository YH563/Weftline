using Weftline.Language.Binding;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language.Projects;

/// <summary>
/// 一个已解析的 `.weft` 文件及其路径。
/// A parsed `.weft` file together with its path.
/// </summary>
public sealed record ProjectFile(
    string FilePath,
    WeftFileSyntax Ast);

/// <summary>
/// 一个模块的定义位置。
/// The definition location of a module.
/// </summary>
public sealed record ChapterDefinition(
    string Title,
    string FilePath,
    TextSpan Span,
    TextSpan TitleSpan);

public sealed record ModuleDefinition(
    string Title,
    string Chapter,
    string FilePath,
    TextSpan Span,
    TextSpan TitleSpan,
    string? Scene);

/// <summary>
/// 项目级索引：合并后的符号、章节、模块和文件列表。
/// A project-level index: merged symbols, chapters, modules, and files.
/// </summary>
public sealed record ProjectIndex(
    IReadOnlyDictionary<string, SymbolEntry> Symbols,
    IReadOnlyDictionary<string, ChapterDefinition> Chapters,
    IReadOnlyDictionary<string, ModuleDefinition> Modules,
    IReadOnlyList<ProjectFile> Files);
