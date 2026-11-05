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
/// 快照中的文件优先使用内存文本，其余文件从磁盘读取。
/// Loads a project from a flat folder. Non-recursive; reads only top-level
/// `.weft` and `.wstory` files. Files in the snapshot use in-memory text; the
/// rest are read from disk.
/// </summary>
public static class ProjectLoader
{
    /// <summary>
    /// 从磁盘文件夹加载项目。
    /// Loads a project from a disk folder.
    /// </summary>
    public static LoadedProject Load(string folder)
        => Load(ProjectSnapshot.FromFolder(folder), null);

    /// <summary>
    /// 从快照加载项目；可选用解析缓存复用 AST。
    /// Loads a project from a snapshot, optionally reusing ASTs from a parse cache.
    /// </summary>
    public static LoadedProject Load(
        ProjectSnapshot snapshot,
        CompilationCache? cache = null)
    {
        var diagnostics = new List<ProjectDiagnostic>();
        var weftFiles = new List<ProjectFile>();

        var documents = snapshot.Documents.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);

        var weftPaths = EnumeratePaths(snapshot.Folder, "*.weft", documents.Keys)
            .OrderBy(static path => path, StringComparer.Ordinal);

        foreach (var path in weftPaths)
        {
            if (!TryReadText(path, documents, out var text))
            {
                continue;
            }

            WeftFileSyntax? weft;
            IReadOnlyList<Diagnostic> parseDiagnostics;

            if (cache is not null &&
                cache.TryGetWeft(path, text, out var cachedWeft, out var cachedDiagnostics))
            {
                weft = cachedWeft;
                parseDiagnostics = cachedDiagnostics;
            }
            else
            {
                var result = WeftlineCompiler.Parse(text, path);
                weft = result.Script as WeftFileSyntax;
                parseDiagnostics = result.Diagnostics;
                cache?.SetWeft(path, text, weft, parseDiagnostics);
            }

            diagnostics.AddRange(parseDiagnostics.Select(diagnostic =>
                new ProjectDiagnostic(path, diagnostic)));

            if (weft is not null)
            {
                weftFiles.Add(new ProjectFile(path, weft));
            }
        }

        var wstoryPath = EnumeratePaths(snapshot.Folder, "*.wstory", documents.Keys)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .FirstOrDefault();

        WStoryFileSyntax? wstory = null;

        if (wstoryPath is not null &&
            TryReadText(wstoryPath, documents, out var wstoryText))
        {
            IReadOnlyList<Diagnostic> parseDiagnostics;

            if (cache is not null &&
                cache.TryGetWStory(wstoryPath, wstoryText, out var cachedWStory, out var cachedDiagnostics))
            {
                wstory = cachedWStory;
                parseDiagnostics = cachedDiagnostics;
            }
            else
            {
                var result = WeftlineCompiler.Parse(wstoryText, wstoryPath);
                wstory = result.Script as WStoryFileSyntax;
                parseDiagnostics = result.Diagnostics;
                cache?.SetWStory(wstoryPath, wstoryText, wstory, parseDiagnostics);
            }

            diagnostics.AddRange(parseDiagnostics.Select(diagnostic =>
                new ProjectDiagnostic(wstoryPath, diagnostic)));
        }

        return new LoadedProject(
            snapshot.Folder,
            wstory,
            wstoryPath,
            weftFiles,
            diagnostics);
    }

    private static IEnumerable<string> EnumeratePaths(
        string folder,
        string pattern,
        IEnumerable<string> snapshotPaths)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);

        if (Directory.Exists(folder))
        {
            foreach (var path in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
            {
                paths.Add(path);
            }
        }

        foreach (var path in snapshotPaths)
        {
            if (path.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static bool TryReadText(
        string path,
        IReadOnlyDictionary<string, string> documents,
        out string text)
    {
        if (documents.TryGetValue(path, out var inMemory))
        {
            text = inMemory;
            return true;
        }

        if (!File.Exists(path))
        {
            text = string.Empty;
            return false;
        }

        text = File.ReadAllText(path);
        return true;
    }

}
