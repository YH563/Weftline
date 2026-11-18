using Weftline.App.Services;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel
{
    public bool HasUnsavedChanges => UnsavedDocumentCount > 0;

    public int UnsavedDocumentCount
        => Editor.Documents.Count(static document => document.IsModified);

    /// <summary>
    /// 有未保存修改时询问是否放弃；返回 false 表示调用方应取消当前操作。
    /// Asks whether to discard unsaved changes; false means the caller should abort.
    /// </summary>
    public async Task<bool> ConfirmDiscardChangesAsync(string action)
    {
        if (!HasUnsavedChanges)
        {
            return true;
        }

        return await _dialogs
            .ConfirmAsync(
                "未保存的修改",
                $"有 {UnsavedDocumentCount} 个文件尚未保存，{action}会丢失这些修改。是否继续？")
            .ConfigureAwait(true);
    }

    private void StartWatching(string folder)
    {
        _projectWatcher?.Dispose();
        _projectWatcher = null;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        _projectWatcher = new ProjectFolderWatcher(folder);
        _projectWatcher.Changed += OnProjectFolderChanged;
    }

    private void OnProjectFolderChanged(IReadOnlyList<string> paths)
    {
        if (_disposed || string.IsNullOrWhiteSpace(_projectFolder))
        {
            return;
        }

        var result = Editor.ReloadDocumentsFromDisk(paths);
        Explorer.RefreshFromDisk();

        Status.SetStatus(result.Conflicts > 0
            ? $"检测到外部修改：{result.Reloaded} 个已重载，{result.Conflicts} 个未保存文件保持不变"
            : "检测到外部文件变化，已重新分析");

        _ = CompileNowAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _statusContextTimer.Stop();
        _statusContextTimer.Tick -= OnStatusContextTimerTick;
        _statusCaretTimer.Stop();
        _statusCaretTimer.Tick -= OnStatusCaretTimerTick;

        _projectWatcher?.Dispose();
        _projectWatcher = null;

        Explorer.FileOpenRequested -= OnFileOpenRequested;
        Explorer.FileSystemChanged -= OnFileSystemChanged;
        Explorer.DeleteConfirmation = null;

        Editor.OpenFailed -= OnEditorOpenFailed;
        Editor.PropertyChanged -= OnEditorPropertyChanged;
        Editor.DocumentTextChanged -= OnDocumentTextChanged;
        Editor.Documents.CollectionChanged -= OnDocumentsCollectionChanged;
        Editor.CloseConfirmation = null;

        if (_observedDocument is not null)
        {
            _observedDocument.PropertyChanged -= OnDocumentPropertyChanged;
            _observedDocument = null;
        }

        _analysisScheduler.Started -= OnAnalysisStarted;
        _analysisScheduler.Completed -= OnAnalysisCompleted;
        _analysisScheduler.Failed -= OnAnalysisFailed;
        _analysisScheduler.Dispose();

        Editor.Dispose();
        Preview.Dispose();
    }
}
