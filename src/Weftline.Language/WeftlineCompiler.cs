using Weftline.Language.Binding;
using Weftline.Language.Diagnostics;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language;

/// <summary>
/// 一次解析的结果。即使存在错误，也会返回当前可用的最佳 AST
/// 以及完整的诊断列表。
/// The result of one parse. Even when there are errors, it returns the best
/// available AST together with the full diagnostic list.
/// </summary>
public sealed class CompilationResult
{
    public CompilationResult(
        SourceText source,
        ScriptFileSyntax script,
        IReadOnlyList<Diagnostic> diagnostics)
    {
        Source = source;
        Script = script;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// 源码文本。
    /// The source text.
    /// </summary>
    public SourceText Source { get; }

    /// <summary>
    /// 解析后的语法树；即使存在错误也会返回。
    /// The parsed syntax tree; returned even when there are errors.
    /// </summary>
    public ScriptFileSyntax Script { get; }

    /// <summary>
    /// 本次解析收集的诊断。
    /// Diagnostics collected during this parse.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// 是否存在错误级别的诊断。
    /// Whether any diagnostic has error severity.
    /// </summary>
    public bool HasErrors => Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// 编译器的公共入口。
///
/// 单文件入口：源码文本 -> AST + 单文件符号绑定 + 诊断。
/// 项目级图与投影由 `ProjectCompiler` 提供。
///
/// Single-file entry point: source text -> AST + single-file binding + diagnostics.
/// Project-level graph and projections are provided by `ProjectCompiler`.
/// </summary>
public static class WeftlineCompiler
{
    /// <summary>
    /// 解析源码文本，只做语法与结构检查。
    /// Parses a source text and performs syntax/structure checks only.
    /// </summary>
    public static CompilationResult Parse(string text, string? filePath = null)
    {
        var source = SourceText.From(text, filePath);
        var diagnostics = new DiagnosticBag();
        var script = Parser.Parse(source, diagnostics);

        return new CompilationResult(source, script, diagnostics.ToList());
    }

    /// <summary>
    /// 解析并绑定符号：语法检查 + 全局符号解析。
    /// Parses and binds symbols: syntax checks plus global symbol resolution.
    /// </summary>
    public static CompilationResult Compile(string text, string? filePath = null)
    {
        var parsed = Parse(text, filePath);
        var diagnostics = new DiagnosticBag();

        foreach (var diagnostic in parsed.Diagnostics)
        {
            diagnostics.Add(diagnostic);
        }

        Binder.Bind(parsed.Script, diagnostics, parsed.Source.FilePath);

        return new CompilationResult(parsed.Source, parsed.Script, diagnostics.ToList());
    }

    /// <summary>
    /// 读取文件并解析，只做语法检查。
    /// Reads a file and parses it with syntax checks only.
    /// </summary>
    public static CompilationResult ParseFile(string filePath)
        => Parse(File.ReadAllText(filePath), filePath);

    /// <summary>
    /// 读取文件并编译：解析 + 符号绑定。
    /// Reads a file and compiles it: parse plus symbol binding.
    /// </summary>
    public static CompilationResult CompileFile(string filePath)
        => Compile(File.ReadAllText(filePath), filePath);
}
