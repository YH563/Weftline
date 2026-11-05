using Weftline.Language.Diagnostics;
using Weftline.Language.Syntax;

namespace Weftline.Language.Projects;

/// <summary>
/// 实时编译的解析缓存：路径 + 文本 → AST + 解析诊断。
///
/// 只缓存解析阶段；绑定、图和投影仍然全量重建。
/// Parse cache for real-time compilation: path + text → AST + parse diagnostics.
///
/// Only the parse stage is cached; binding, graph, and projections are rebuilt.
/// </summary>
public sealed class CompilationCache
{
    private readonly Dictionary<string, CachedWeft> _weft = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CachedWStory> _wstory = new(StringComparer.Ordinal);

    /// <summary>
    /// 尝试复用文本未变化的 `.weft` 解析结果。
    /// Tries to reuse a parsed `.weft` result when the text is unchanged.
    /// </summary>
    public bool TryGetWeft(
        string path,
        string text,
        out WeftFileSyntax? ast,
        out IReadOnlyList<Diagnostic> diagnostics)
    {
        if (_weft.TryGetValue(Normalize(path), out var cached) &&
            string.Equals(cached.Text, text, StringComparison.Ordinal))
        {
            ast = cached.Ast;
            diagnostics = cached.Diagnostics;
            return true;
        }

        ast = null;
        diagnostics = [];
        return false;
    }

    /// <summary>
    /// 缓存一次 `.weft` 解析结果。
    /// Caches one parsed `.weft` result.
    /// </summary>
    public void SetWeft(
        string path,
        string text,
        WeftFileSyntax? ast,
        IReadOnlyList<Diagnostic> diagnostics)
        => _weft[Normalize(path)] = new CachedWeft(text, ast, diagnostics);

    /// <summary>
    /// 尝试复用文本未变化的 `.wstory` 解析结果。
    /// Tries to reuse a parsed `.wstory` result when the text is unchanged.
    /// </summary>
    public bool TryGetWStory(
        string path,
        string text,
        out WStoryFileSyntax? ast,
        out IReadOnlyList<Diagnostic> diagnostics)
    {
        if (_wstory.TryGetValue(Normalize(path), out var cached) &&
            string.Equals(cached.Text, text, StringComparison.Ordinal))
        {
            ast = cached.Ast;
            diagnostics = cached.Diagnostics;
            return true;
        }

        ast = null;
        diagnostics = [];
        return false;
    }

    /// <summary>
    /// 缓存一次 `.wstory` 解析结果。
    /// Caches one parsed `.wstory` result.
    /// </summary>
    public void SetWStory(
        string path,
        string text,
        WStoryFileSyntax? ast,
        IReadOnlyList<Diagnostic> diagnostics)
        => _wstory[Normalize(path)] = new CachedWStory(text, ast, diagnostics);

    /// <summary>
    /// 清空全部解析缓存。
    /// Clears all parse cache entries.
    /// </summary>
    public void Clear()
    {
        _weft.Clear();
        _wstory.Clear();
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path);

    private sealed record CachedWeft(
        string Text,
        WeftFileSyntax? Ast,
        IReadOnlyList<Diagnostic> Diagnostics);

    private sealed record CachedWStory(
        string Text,
        WStoryFileSyntax? Ast,
        IReadOnlyList<Diagnostic> Diagnostics);
}
