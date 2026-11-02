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
/// 目前提供源码解析：源码文本 -> AST + 诊断。
/// 语义解析和 StoryGraph IR 尚未接入。
///
/// Public entry point for the compiler.
///
/// Currently provides source parsing: source text -> AST + diagnostics.
/// Semantic resolution and StoryGraph IR are not included yet.
/// </summary>
public static class WeftlineCompiler
{
    /// <summary>
    /// 解析源码文本。
    /// Parses a source text.
    /// </summary>
    public static CompilationResult Parse(string text, string? filePath = null)
    {
        var source = SourceText.From(text, filePath);
        var diagnostics = new DiagnosticBag();
        var script = Parser.Parse(source, diagnostics);

        return new CompilationResult(source, script, diagnostics.ToList());
    }

    /// <summary>
    /// 读取文件并解析。
    /// Reads a file and parses it.
    /// </summary>
    public static CompilationResult ParseFile(string filePath)
        => Parse(File.ReadAllText(filePath), filePath);
}
