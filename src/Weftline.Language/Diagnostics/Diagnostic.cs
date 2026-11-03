using Weftline.Language.Source;

namespace Weftline.Language.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// 编译器诊断：代码、严重级别、消息和源码范围。
/// 编译器不会因用户错误而抛出异常，而是收集诊断。
/// A compiler diagnostic: code, severity, message, and source span.
/// The compiler does not throw on user errors; it collects diagnostics.
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
/// 收集诊断。解析器和后续校验阶段写入同一个 bag。
/// Collects diagnostics. The parser and later validation stages write into the same bag.
/// </summary>
public sealed class DiagnosticBag : IEnumerable<Diagnostic>
{
    private readonly List<Diagnostic> _items = [];

    /// <summary>
    /// 诊断数量。
    /// Number of diagnostics.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// 是否存在错误级别的诊断。
    /// Whether any diagnostic has error severity.
    /// </summary>
    public bool HasErrors => _items.Any(static d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// 返回诊断的只读快照。
    /// Returns a read-only snapshot of the diagnostics.
    /// </summary>
    public IReadOnlyList<Diagnostic> ToList() => _items.ToList();

    /// <summary>
    /// 添加一条诊断。
    /// Adds a diagnostic.
    /// </summary>
    public void Add(Diagnostic diagnostic) => _items.Add(diagnostic);

    /// <summary>
    /// 添加一条错误诊断。
    /// Adds an error diagnostic.
    /// </summary>
    public void Error(string code, TextSpan span, string message)
        => Add(new Diagnostic(code, DiagnosticSeverity.Error, message, span));

    /// <summary>
    /// 添加一条警告诊断。
    /// Adds a warning diagnostic.
    /// </summary>
    public void Warning(string code, TextSpan span, string message)
        => Add(new Diagnostic(code, DiagnosticSeverity.Warning, message, span));

    /// <summary>
    /// 添加一条信息诊断。
    /// Adds an informational diagnostic.
    /// </summary>
    public void Info(string code, TextSpan span, string message)
        => Add(new Diagnostic(code, DiagnosticSeverity.Info, message, span));

    public IEnumerator<Diagnostic> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// 诊断代码。
/// Diagnostic codes.
/// </summary>
public static class DiagnosticCode
{
    public const string UnknownLine = "WFT1001";
    public const string UnterminatedTextBlock = "WFT1002";
    public const string MalformedDirective = "WFT1003";
    public const string MalformedExitBranch = "WFT1004";
    public const string InvalidTitleCharacter = "WFT1005";
    public const string UnterminatedReference = "WFT1006";

    public const string VarOutsideGlobals = "WFT2001";
    public const string MultipleGlobals = "WFT2002";
    public const string GlobalsNotAtFileHeader = "WFT2003";
    public const string DuplicateGlobalVariable = "WFT2004";
    public const string DuplicateModuleTitle = "WFT2005";
    public const string HeaderAfterContent = "WFT2006";
    public const string EmptyTitle = "WFT2007";
    public const string UndeclaredCharacter = "WFT2008";
    public const string DuplicateCharacter = "WFT2009";
    public const string DuplicateItem = "WFT2010";
    public const string DuplicateSymbol = "WFT2011";
    public const string SetInProjectFile = "WFT2012";
    public const string SetInContent = "WFT2013";
    public const string UndeclaredReference = "WFT2014";
    public const string DuplicateScene = "WFT2015";
    public const string MalformedScene = "WFT2016";
    public const string MissingChapterTitle = "WFT2017";
    public const string MultipleChapterTitle = "WFT2018";
    public const string DuplicateChapterTitle = "WFT2019";

    public const string MissingExitBlock = "WFT3001";
    public const string MissingFallback = "WFT3002";
    public const string ModeMismatch = "WFT3003";
    public const string InvalidBranchLabel = "WFT3004";
    public const string ContentAfterExit = "WFT3005";
    public const string UndeclaredConditionVariable = "WFT3006";
    public const string UndeclaredVariable = "WFT3007";
    public const string InvalidCondition = "WFT3008";
    public const string MalformedEffect = "WFT3009";
    public const string DuplicateEffectAssignment = "WFT3010";

    public const string TargetNotFound = "WFT4001";
    public const string InvalidEnd = "WFT4002";
    public const string EmptyProject = "WFT4003";
    public const string UnreachableModule = "WFT4004";
    public const string PathCannotReachEnd = "WFT4005";
    public const string ExternalTargetInIsolation = "WFT4006";
    public const string MissingAsset = "WFT4007";
    public const string UnusedGlobalVariable = "WFT4008";
}
