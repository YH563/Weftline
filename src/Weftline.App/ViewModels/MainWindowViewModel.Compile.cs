using System.Text;
using Weftline.App.Features.Editor.Services;
using Weftline.App.Features.Editor.ViewModels;
using Weftline.App.Services;
using Weftline.Core;
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

    private Dictionary<string, string> CreateDocumentMap()
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var document in Editor.Documents)
        {
            if (string.IsNullOrWhiteSpace(document.FilePath) ||
                !WeftDocumentKinds.IsWeftFile(document.FilePath))
            {
                continue;
            }

            if (_documentSnapshots.TryGetValue(document.FilePath, out var snapshot) &&
                ReferenceEquals(snapshot.Owner, document) &&
                snapshot.Version == document.Version)
            {
                documents[document.FilePath] = snapshot.Text;
                continue;
            }

            var text = document.GetTextSnapshot();
            _documentSnapshots[document.FilePath] = new DocumentSnapshotEntry(
                document,
                document.Version,
                text);
            documents[document.FilePath] = text;
        }

        return documents;
    }

    private readonly record struct DocumentSnapshotEntry(
        EditorDocumentViewModel Owner,
        long Version,
        string Text);

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

        var positions = BuildPositionCache(
            native.Diagnostics.Select(static item => item.FilePath),
            output.Documents);
        UpdateDiagnostics(native.Diagnostics.Select(item => CreateDiagnostic(item, positions)));
        Editor.SetDiagnostics(native.Diagnostics.Select(ToWeftDiagnostic).ToList());

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

    private void UpdateDiagnostics(IEnumerable<DiagnosticItem> diagnostics)
    {
        Diagnostics.Clear();

        foreach (var diagnostic in diagnostics)
        {
            Diagnostics.Add(diagnostic);
        }
    }

    private Dictionary<string, LineIndex> BuildPositionCache(
        IEnumerable<string> filePaths,
        IReadOnlyDictionary<string, string> documents)
    {
        var cache = new Dictionary<string, LineIndex>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(path) || cache.ContainsKey(path))
            {
                continue;
            }

            var text = documents.TryGetValue(path, out var snapshot)
                ? snapshot
                : GetDocumentText(path);

            if (text is not null)
            {
                cache[path] = new LineIndex(text);
            }
        }

        return cache;
    }

    private static DiagnosticItem CreateDiagnostic(
        NativeDiagnostic diagnostic,
        IReadOnlyDictionary<string, LineIndex> positions)
    {
        var (line, character) = positions.TryGetValue(diagnostic.FilePath, out var index)
            ? index.Resolve(diagnostic.Start)
            : (1, 1);

        return new DiagnosticItem(
            diagnostic.FilePath,
            line,
            character,
            diagnostic.Severity,
            diagnostic.Code,
            diagnostic.Message);
    }

    private static WeftDiagnostic ToWeftDiagnostic(NativeDiagnostic diagnostic)
        => new(
            diagnostic.FilePath,
            new TextSpan(diagnostic.Start, diagnostic.Length),
            diagnostic.Severity switch
            {
                "error" => WeftDiagnosticSeverity.Error,
                "warning" => WeftDiagnosticSeverity.Warning,
                _ => WeftDiagnosticSeverity.Info,
            },
            diagnostic.Code,
            diagnostic.Message);

    private sealed class LineIndex
    {
        private readonly int[] _starts;
        private readonly int _length;

        public LineIndex(string text)
        {
            _length = text.Length;

            var starts = new List<int> { 0 };
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '\n')
                {
                    starts.Add(index + 1);
                }
            }

            _starts = starts.ToArray();
        }

        public (int Line, int Character) Resolve(int offset)
        {
            offset = Math.Clamp(offset, 0, _length);

            var index = Array.BinarySearch(_starts, offset);
            if (index < 0)
            {
                index = ~index - 1;
            }

            return (index + 1, offset - _starts[index] + 1);
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
