using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Features.Editor.Services;
using Weftline.App.Features.Editor.ViewModels;
using Weftline.App.Features.Graph;
using Weftline.App.Features.Graph.Layout;
using Weftline.App.Features.Graph.ViewModels;
using Weftline.App.Features.Player.ViewModels;
using Weftline.App.Models;
using Weftline.App.Services;
using Weftline.Core;
using Weftline.Core.Language;
using Weftline.Core.Runtime;
using Weftline.Native;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel
{
    private async Task CompileNowAsync(bool includeRuntime = false)
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.UpdateProject(null);
            return;
        }

        await _analysisScheduler
            .RunNowAsync(
                _projectFolder,
                reloadFromDisk: true,
                buildRuntime: includeRuntime || IsPreviewVisible)
            .ConfigureAwait(true);
    }

    private void OnAnalysisStarted()
        => Status.SetStatus("正在分析项目…");

    private void OnAnalysisCompleted(ProjectAnalysisResult result)
        => ApplyNativeResult(result);

    private void OnAnalysisFailed(Exception exception)
    {
        AppLog.Error(exception, "项目分析失败");
        UpdateDiagnostics([]);
        Editor.SetDiagnostics([]);
        _lastSymbols = null;
        _editorLanguageService.Projection = null;
        _editorLanguageService.Symbols = null;
        Status.UpdateProject(null);
        Status.SetStatus($"分析失败：{exception.Message}");

        if (_lastRuntime is not null && _lastGoodProjection is not null)
        {
            Preview.UpdateProject(
                _projectFolder ?? string.Empty,
                _lastRuntime,
                _lastGoodProjection,
                isStale: true);
        }
        else
        {
            Preview.Reset();
        }
    }

    private void ApplyNativeResult(ProjectAnalysisResult output)
    {
        var native = output.Native;
        var projection = output.Projection;
        _lastSymbols = output.Symbols;
        _editorLanguageService.Projection = projection;
        _editorLanguageService.Symbols = output.Symbols;

        var errors = native.Diagnostics.Count(static item => item.Severity == "error");
        var warnings = native.Diagnostics.Count(static item => item.Severity == "warning");
        var variableCount = native.Graph?.Globals.Count ?? 0;
        var moduleCount = projection?.Modules.Count ?? 0;

        UpdateDiagnostics(DiagnosticPresentation.CreateItems(
            native.Diagnostics,
            output.Documents,
            GetDocumentText));
        Editor.SetDiagnostics(
            native.Diagnostics.Select(DiagnosticPresentation.ToWeftDiagnostic).ToList());

        Status.UpdateProject(new ProjectCompileSummary(
            moduleCount, variableCount, errors, warnings));
        Status.SetStatus(errors == 0 ? "就绪" : $"分析完成：{errors} 个错误");

        if (output.Runtime is not null)
        {
            _lastRuntime = output.Runtime;
        }

        if (IsPreviewVisible)
        {
            Preview.UpdateProject(
                _projectFolder ?? string.Empty,
                output.Runtime ?? _lastRuntime,
                projection ?? _lastGoodProjection,
                isStale: output.Runtime is null && errors > 0);
            UpdatePreviewFile();
        }

        if (projection is { } available && moduleCount > 0)
        {
            UpdateGraphProjection(available);
            _lastGoodProjection = available;
            Graph.StatusText = $"{moduleCount} 模块 · {errors} 错误 · {warnings} 警告";
            return;
        }

        if (_lastGoodProjection is { } lastProjection)
        {
            UpdateGraphProjection(lastProjection);
            _editorLanguageService.Projection = lastProjection;
            Graph.StatusText = $"保留上一次可用图 · {errors} 错误 · {warnings} 警告";
            return;
        }

        UpdateGraphProjection(projection);
        Graph.StatusText = errors == 0 ? "暂无模块" : $"{errors} 错误 · {warnings} 警告";
    }

    private void UpdateGraphProjection(GraphProjection? projection)
    {
        var signature = projection is null ? null : GraphProjectionSignature.Build(projection);

        if (string.Equals(signature, _lastGraphSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastGraphSignature = signature;
        Graph.Projection = projection;
    }

    /// <summary>
    /// 打开诊断对应的文件并定位到源码位置。

    private void UpdateDiagnostics(IEnumerable<DiagnosticItem> diagnostics)
    {
        Diagnostics.Clear();

        foreach (var diagnostic in diagnostics)
        {
            Diagnostics.Add(diagnostic);
        }
    }

    private string? GetDocumentText(string filePath)
    {
        var document = Editor.Documents.FirstOrDefault(candidate =>
            string.Equals(candidate.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (document is not null)
        {
            return document.GetTextSnapshot();
        }

        try
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetFolderName(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? folder : name;
    }
}
