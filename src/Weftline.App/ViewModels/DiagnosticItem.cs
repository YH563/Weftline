namespace Weftline.App.ViewModels;

/// <summary>
/// 一条用于界面展示的诊断。
/// </summary>
public sealed record DiagnosticItem(
    string FilePath,
    int Line,
    int Character,
    string Severity,
    string Code,
    string Message)
{
    /// <summary>
    /// 用于列表显示的短位置文本。
    /// </summary>
    public string Location => $"{Path.GetFileName(FilePath)}:{Line}:{Character}";

    public bool IsError => string.Equals(Severity, "error", StringComparison.OrdinalIgnoreCase);

    public bool IsWarning => string.Equals(Severity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsInfo => string.Equals(Severity, "info", StringComparison.OrdinalIgnoreCase);
}
