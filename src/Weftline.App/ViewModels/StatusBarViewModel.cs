using CommunityToolkit.Mvvm.ComponentModel;
using Weftline.Editor.ViewModels;

namespace Weftline.App.ViewModels;

/// <summary>
/// 状态栏需要的编译摘要，不依赖 C# 或 C++ 的具体结果类型。
/// </summary>
public readonly record struct ProjectCompileSummary(
    int ModuleCount,
    int VariableCount,
    int Errors,
    int Warnings);

public partial class StatusBarViewModel : ObservableObject
{
    [ObservableProperty]
    private string _caretText = string.Empty;

    [ObservableProperty]
    private string _selectionText = string.Empty;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _language = "—";

    [ObservableProperty]
    private string _contextText = "未打开文件";

    [ObservableProperty]
    private string _symbolText = "模块 - · 变量 -";

    [ObservableProperty]
    private string _diagnosticsText = "无诊断";

    [ObservableProperty]
    private string _statusText = "就绪";

    /// <summary>
    /// 根据当前文档更新行列、语言和模块上下文。
    /// </summary>
    public void UpdateDocument(EditorDocumentViewModel? document)
    {
        if (document is null)
        {
            CaretText = string.Empty;
            SelectionText = string.Empty;
            HasSelection = false;
            Language = "—";
            ContextText = "未打开文件";
            return;
        }

        CaretText = $"行 {document.CaretLine}，列 {document.CaretColumn}";
        HasSelection = document.SelectionLength > 0;
        SelectionText = HasSelection ? $"已选 {document.SelectionLength} 字符" : string.Empty;
        Language = ResolveLanguage(document.FilePath);
        ContextText = ResolveContext(document);
    }

    /// <summary>
    /// 更新模块/变量数量和诊断摘要；null 表示清空。
    /// </summary>
    public void UpdateProject(ProjectCompileSummary? summary)
    {
        if (summary is not { } value)
        {
            SymbolText = "模块 - · 变量 -";
            DiagnosticsText = "无诊断";
            return;
        }

        SymbolText = $"模块 {value.ModuleCount} · 变量 {value.VariableCount}";
        DiagnosticsText = value.Errors == 0 && value.Warnings == 0
            ? "无诊断"
            : $"{value.Errors} 错误 · {value.Warnings} 警告";
    }

    /// <summary>
    /// 设置状态栏短消息。
    /// </summary>
    public void SetStatus(string message) => StatusText = message;

    private static string ResolveLanguage(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".weft" => "Weft",
            ".wstory" => "WStory",
            _ => "纯文本",
        };

    private static string ResolveContext(EditorDocumentViewModel document)
    {
        if (Path.GetExtension(document.FilePath).Equals(".wstory", StringComparison.OrdinalIgnoreCase))
        {
            return "项目总览";
        }

        if (string.IsNullOrWhiteSpace(document.Text))
        {
            return "无模块";
        }

        var lines = document.Text.Split('\n');
        var limit = Math.Clamp(document.CaretLine, 1, lines.Length);
        string? module = null;
        string? scene = null;

        for (var index = 0; index < limit; index++)
        {
            var line = lines[index].Trim();

            if (line.StartsWith("@module ", StringComparison.Ordinal))
            {
                module = line["@module ".Length..].Trim();
                scene = null;
            }
            else if (line.StartsWith("@scene ", StringComparison.Ordinal))
            {
                scene = line["@scene ".Length..].Trim();
            }
        }

        if (module is null && scene is null)
        {
            return "无模块";
        }

        if (module is null)
        {
            return $"场景: {scene}";
        }

        return scene is null ? $"模块: {module}" : $"模块: {module} · 场景: {scene}";
    }
}
