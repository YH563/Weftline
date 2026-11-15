namespace Weftline.Core.Language;

/// <summary>
/// native 诊断严重度字符串到领域枚举的映射；C# 与 native 共享同一套取值。
/// </summary>
public static class WeftDiagnosticSeverityMapping
{
    /// <summary>把 native 的 "error"/"warning"/其它 映射为领域枚举。</summary>
    public static WeftDiagnosticSeverity FromNative(string? severity)
        => severity switch
        {
            "error" => WeftDiagnosticSeverity.Error,
            "warning" => WeftDiagnosticSeverity.Warning,
            _ => WeftDiagnosticSeverity.Info,
        };
}
