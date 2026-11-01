using Weftline.Language.Diagnostics;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language;

/// <summary>
/// 一次解析的结果。即使有错误，也尽量返回可用的 AST 和完整的诊断列表。
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

    public SourceText Source { get; }

    public ScriptFileSyntax Script { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool HasErrors => Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// 编译器的对外入口。
///
/// Step 1 只做“源文本 -> AST + 诊断”。
/// Step 2 会在此基础上增加出口分支解析、目标解析与 StoryGraph IR。
/// </summary>
public static class WeftlineCompiler
{
    public static CompilationResult Parse(string text, string? filePath = null)
    {
        var source = SourceText.From(text, filePath);
        var diagnostics = new DiagnosticBag();
        var script = Parser.Parse(source, diagnostics);

        return new CompilationResult(source, script, diagnostics.ToList());
    }

    public static CompilationResult ParseFile(string filePath)
        => Parse(File.ReadAllText(filePath), filePath);
}
