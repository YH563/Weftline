using CommunityToolkit.Mvvm.ComponentModel;
using Weftline.Language.Binding;
using Weftline.Language.Diagnostics;
using Weftline.Language.Projects;

namespace Weftline.App.ViewModels;

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

    public void UpdateDocument(EditorViewModel? document)
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

    public void UpdateProject(ProjectCompilationResult? result)
    {
        if (result is null)
        {
            SymbolText = "模块 - · 变量 -";
            DiagnosticsText = "无诊断";
            return;
        }

        var moduleCount = result.Index.Modules.Count;
        var variableCount = result.Index.Symbols.Values.Count(static symbol => symbol.Kind == SymbolKind.Variable);
        var errors = result.Diagnostics.Count(static item => item.Diagnostic.Severity == DiagnosticSeverity.Error);
        var warnings = result.Diagnostics.Count(static item => item.Diagnostic.Severity == DiagnosticSeverity.Warning);

        SymbolText = $"模块 {moduleCount} · 变量 {variableCount}";
        DiagnosticsText = errors == 0 && warnings == 0
            ? "无诊断"
            : $"{errors} 错误 · {warnings} 警告";
    }

    public void SetStatus(string message) => StatusText = message;

    private static string ResolveLanguage(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".weft" => "Weft",
            ".wstory" => "WStory",
            _ => "纯文本",
        };

    private static string ResolveContext(EditorViewModel document)
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
