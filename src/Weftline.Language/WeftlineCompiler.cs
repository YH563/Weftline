using Weftline.Language.Diagnostics;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language;

/// <summary>
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

    /// <summary>The source text.</summary>
    public SourceText Source { get; }

    /// <summary>The parsed syntax tree; returned even when there are errors.</summary>
    public ScriptFileSyntax Script { get; }

    /// <summary>Diagnostics collected during this parse.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>Whether any diagnostic has error severity.</summary>
    public bool HasErrors => Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// Public entry point for the compiler.
///
/// Currently provides source parsing: source text -> AST + diagnostics.
/// Semantic resolution and StoryGraph IR are not included yet.
/// </summary>
public static class WeftlineCompiler
{
    /// <summary>Parses a source text.</summary>
    public static CompilationResult Parse(string text, string? filePath = null)
    {
        var source = SourceText.From(text, filePath);
        var diagnostics = new DiagnosticBag();
        var script = Parser.Parse(source, diagnostics);

        return new CompilationResult(source, script, diagnostics.ToList());
    }

    /// <summary>Reads a file and parses it.</summary>
    public static CompilationResult ParseFile(string filePath)
        => Parse(File.ReadAllText(filePath), filePath);
}
