using Weftline.Language.Diagnostics;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language.Projects;

/// <summary>
/// 一条携带文件路径的项目诊断；可选关联位置指向首次声明。
/// A project diagnostic paired with its file path; the optional related
/// location points to the first declaration.
/// </summary>
public sealed record ProjectDiagnostic(
    string FilePath,
    Diagnostic Diagnostic,
    SourceLocation? Related = null);

/// <summary>
/// 一个已加载的项目：一个 `.wstory` 和若干 `.weft`。
/// A loaded project: one `.wstory` and zero or more `.weft` files.
/// </summary>
public sealed class LoadedProject
{
    public LoadedProject(
        string folder,
        WStoryFileSyntax? wstory,
        string? wstoryPath,
        IReadOnlyList<ProjectFile> weftFiles,
        IReadOnlyList<ProjectDiagnostic> diagnostics)
    {
        Folder = folder;
        WStory = wstory;
        WStoryPath = wstoryPath;
        WeftFiles = weftFiles;
        Diagnostics = diagnostics;
    }

    public string Folder { get; }

    public WStoryFileSyntax? WStory { get; }

    public string? WStoryPath { get; }

    public IReadOnlyList<ProjectFile> WeftFiles { get; }

    public IReadOnlyList<ProjectDiagnostic> Diagnostics { get; }
}

/// <summary>
/// 从扁平文件夹加载项目。非递归，只读取顶层 `.weft` 和 `.wstory`。
/// Loads a project from a flat folder. Non-recursive; reads only top-level
/// `.weft` and `.wstory` files.
/// </summary>
public static class ProjectLoader
{
    public static LoadedProject Load(string folder)
    {
        var diagnostics = new List<ProjectDiagnostic>();
        var weftFiles = new List<ProjectFile>();

        var weftPaths = Directory
            .EnumerateFiles(folder, "*.weft", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal);

        foreach (var path in weftPaths)
        {
            var result = WeftlineCompiler.ParseFile(path);
            diagnostics.AddRange(result.Diagnostics.Select(diagnostic => new ProjectDiagnostic(path, diagnostic)));

            if (result.Script is WeftFileSyntax weft)
            {
                weftFiles.Add(new ProjectFile(path, weft));
            }
        }

        var wstoryPath = Directory
            .EnumerateFiles(folder, "*.wstory", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        WStoryFileSyntax? wstory = null;

        if (wstoryPath is not null)
        {
            var result = WeftlineCompiler.ParseFile(wstoryPath);
            diagnostics.AddRange(result.Diagnostics.Select(diagnostic => new ProjectDiagnostic(wstoryPath, diagnostic)));
            wstory = result.Script as WStoryFileSyntax;
        }

        return new LoadedProject(folder, wstory, wstoryPath, weftFiles, diagnostics);
    }
}
