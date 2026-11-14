using Avalonia.Threading;
using Weftline.Editor.ViewModels;

namespace Weftline.Editor.Services;

/// <summary>
/// 单文件语法分析调度：按文档防抖、取消，并在应用结果前校验文档版本。
/// </summary>
public sealed class SyntaxAnalysisScheduler
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(100);

    private readonly IWeftEditorLanguageService _languageService;
    private readonly object _sync = new();
    private readonly Dictionary<EditorDocumentViewModel, CancellationTokenSource> _pending = [];

    public SyntaxAnalysisScheduler(IWeftEditorLanguageService languageService)
    {
        _languageService = languageService;
    }

    public void Schedule(EditorDocumentViewModel document)
    {
        if (!WeftDocumentKinds.IsWeftFile(document.FilePath))
        {
            Cancel(document);
            document.SetSyntaxDiagnostics([]);
            return;
        }

        CancellationTokenSource cts;

        lock (_sync)
        {
            if (_pending.Remove(document, out var previous))
            {
                previous.Cancel();
            }

            cts = new CancellationTokenSource();
            _pending[document] = cts;
        }

        _ = RunAsync(document, cts);
    }

    public void Cancel(EditorDocumentViewModel document)
    {
        lock (_sync)
        {
            if (_pending.Remove(document, out var cts))
            {
                cts.Cancel();
            }
        }
    }

    public void CancelAll()
    {
        lock (_sync)
        {
            foreach (var cts in _pending.Values)
            {
                cts.Cancel();
            }

            _pending.Clear();
        }
    }

    private async Task RunAsync(
        EditorDocumentViewModel document,
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(DebounceDelay, cts.Token).ConfigureAwait(false);

            var snapshot = await Dispatcher.UIThread.InvokeAsync(() => (
                Version: document.Version,
                document.FilePath,
                Text: document.GetTextSnapshot()));

            if (cts.IsCancellationRequested || string.IsNullOrWhiteSpace(snapshot.FilePath))
            {
                return;
            }

            var diagnostics = await _languageService.GetSyntaxDiagnosticsAsync(
                snapshot.FilePath,
                snapshot.Text,
                snapshot.Version,
                cts.Token).ConfigureAwait(false);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!cts.IsCancellationRequested && document.Version == snapshot.Version)
                {
                    document.SetSyntaxDiagnostics(diagnostics);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            lock (_sync)
            {
                if (_pending.TryGetValue(document, out var current) &&
                    ReferenceEquals(current, cts))
                {
                    _pending.Remove(document);
                }
            }

            cts.Dispose();
        }
    }
}
