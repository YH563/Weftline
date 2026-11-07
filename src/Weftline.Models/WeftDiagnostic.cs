namespace Weftline.Models;

public enum WeftDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public sealed record WeftDiagnostic(
    string FilePath,
    TextSpan Span,
    WeftDiagnosticSeverity Severity,
    string Code,
    string Message);
