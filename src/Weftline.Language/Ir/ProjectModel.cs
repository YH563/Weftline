using Weftline.Language.Source;

namespace Weftline.Language.Ir;

/// <summary>
/// 源级语义模型：项目信息、章节、声明和描述。
/// Source-level semantic model: project info, chapters, declarations, and descriptions.
/// </summary>
public sealed record ProjectModel(
    IrProjectInfo Project,
    IReadOnlyList<ProjectChapter> Chapters,
    ProjectSymbols Symbols);

public sealed record IrProjectInfo(
    string Name,
    string? Summary,
    IReadOnlyList<IrDoc> Docs);

/// <summary>
/// 一条世界设定文档，来自 `.wstory` 的 @doc。
///
/// 只保存标题和正文；不参与图结构、条件判断或播放执行。
/// 完整剧本在 `.weft`，实体短描述在 `ProjectSymbols` 中。
///
/// A world-setting document from @doc in `.wstory`.
///
/// It keeps only the title and body; it does not participate in the graph,
/// conditions, or playback. Script text lives in `.weft`, and short entity
/// descriptions live in `ProjectSymbols`.
/// </summary>
public sealed record IrDoc(
    string Title,
    string Text);

/// <summary>
/// 一个章节：标题、描述、所属模块（有序）和源码位置。
/// A chapter: title, description, ordered module IDs, and source location.
/// </summary>
public sealed record ProjectChapter(
    string Id,
    string Title,
    string? Description,
    IReadOnlyList<string> ModuleIds,
    SourceLocation Source);

/// <summary>
/// 项目级声明。所有列表保持源文件中的出现顺序。
/// Project-level declarations. All lists keep source order.
/// </summary>
public sealed record ProjectSymbols(
    IReadOnlyList<ProjectVariable> Variables,
    IReadOnlyList<ProjectCharacter> Characters,
    IReadOnlyList<ProjectItem> Items,
    IReadOnlyList<ProjectScene> Scenes);

public sealed record ProjectVariable(
    string Name,
    bool Initial,
    string? Description);

public sealed record ProjectCharacter(
    string Name,
    string? Description);

public sealed record ProjectItem(
    string Name,
    string? Description);

public sealed record ProjectScene(
    string Name,
    string? Description);
