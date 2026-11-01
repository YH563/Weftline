using Weftline.Language.Source;

namespace Weftline.Language.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// 一条编译诊断：错误码、级别、消息、源码位置。
/// 编译器遇到用户写错时不会抛异常，而是尽量收集成诊断。
/// </summary>
public sealed record Diagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    TextSpan Span)
{
    public override string ToString() => $"{Code} {Severity}: {Message} @ {Span}";
}

/// <summary>
/// 诊断的收集器。解析器和后续的校验器都往同一个袋子里写。
/// </summary>
public sealed class DiagnosticBag : IEnumerable<Diagnostic>
{
    private readonly List<Diagnostic> _items = [];

    public int Count => _items.Count;

    public bool HasErrors => _items.Any(static d => d.Severity == DiagnosticSeverity.Error);

    public IReadOnlyList<Diagnostic> ToList() => _items;

    public void Add(Diagnostic diagnostic) => _items.Add(diagnostic);

    public void Error(string code, TextSpan span, string message)
        => Add(new Diagnostic(code, DiagnosticSeverity.Error, message, span));

    public void Warning(string code, TextSpan span, string message)
        => Add(new Diagnostic(code, DiagnosticSeverity.Warning, message, span));

    public void Info(string code, TextSpan span, string message)
        => Add(new Diagnostic(code, DiagnosticSeverity.Info, message, span));

    public IEnumerator<Diagnostic> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Step 1 使用的诊断码。后续步骤会继续往这里追加。
/// </summary>
public static class DiagnosticCode
{
    public const string UnknownLine = "WFT1001";
    public const string UnterminatedTextBlock = "WFT1002";
    public const string MalformedDirective = "WFT1003";

    public const string VarOutsideGlobals = "WFT2001";
    public const string MultipleGlobals = "WFT2002";
    public const string GlobalsNotAtFileHeader = "WFT2003";
    public const string DuplicateGlobalVariable = "WFT2004";
    public const string DuplicateModuleTitle = "WFT2005";
    public const string HeaderAfterContent = "WFT2006";
    public const string EmptyTitle = "WFT2007";
    public const string UndeclaredCharacter = "WFT2008";
    public const string DuplicateCharacter = "WFT2009";

    public const string MissingExitBlock = "WFT3001";
    public const string UndeclaredVariable = "WFT3007";
}
