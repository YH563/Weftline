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
    private void OnFileOpenRequested(object? sender, string path) => Editor.OpenFile(path);

    private async void OnEditorOpenFailed(object? sender, EditorOpenFailedEventArgs e)
    {
        Status.SetStatus($"打开失败：{Path.GetFileName(e.FilePath)}");

        try
        {
            await _dialogs
                .ShowMessageAsync("无法打开文件", e.Error.Message)
                .ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, "显示打开失败提示时出错");
        }
    }

    private void OnDocumentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _snapshots.Clear();

        if (ContainsWeftDocument(e.OldItems) || ContainsWeftDocument(e.NewItems))
        {
            ScheduleCompile();
        }
    }

    private static bool ContainsWeftDocument(System.Collections.IList? documents)
        => documents?.Cast<EditorDocumentViewModel>()
            .Any(static document => WeftDocumentKinds.IsWeftFile(document.FilePath)) == true;


    private void OnFileSystemChanged(object? sender, ProjectFileChange change)
    {
        if (change.Kind == ProjectFileChangeKind.Deleted)
        {
            Editor.CloseDocumentsUnder(change.Path);
        }
        else if (change.Kind == ProjectFileChangeKind.Renamed && change.OldPath is not null)
        {
            Editor.MoveDocuments(change.OldPath, change.Path);
        }

        _ = HandleFileSystemChangedAsync(change);
    }

    private async Task HandleFileSystemChangedAsync(ProjectFileChange change)
    {
        await CompileNowAsync().ConfigureAwait(true);
        Status.SetStatus(change.Kind switch
        {
            ProjectFileChangeKind.Created => $"已新建：{Path.GetFileName(change.Path)}",
            ProjectFileChangeKind.Renamed => $"已重命名：{Path.GetFileName(change.Path)}",
            ProjectFileChangeKind.Deleted => $"已删除：{Path.GetFileName(change.Path)}",
            _ => "文件已更新",
        });
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditorHostViewModel.SelectedDocument))
        {
            return;
        }

        _statusContextTimer.Stop();
        _statusCaretTimer.Stop();
        _pendingStatusDocument = null;
        _pendingCaretDocument = null;
        AttachDocument(Editor.SelectedDocument);
        Status.UpdateDocument(Editor.SelectedDocument);
        UpdatePreviewFile();
    }

    private void AttachDocument(EditorDocumentViewModel? document)
    {
        if (_observedDocument is not null)
        {
            _observedDocument.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _observedDocument = document;

        if (_observedDocument is not null)
        {
            _observedDocument.PropertyChanged += OnDocumentPropertyChanged;
        }
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not EditorDocumentViewModel document)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(EditorDocumentViewModel.CaretLine):
            case nameof(EditorDocumentViewModel.CaretColumn):
            case nameof(EditorDocumentViewModel.SelectionLength):
                ScheduleStatus(document);
                break;
            case nameof(EditorDocumentViewModel.FilePath):
                Status.UpdateDocument(document);
                UpdatePreviewFile();
                break;
        }
    }

    private void OnStatusCaretTimerTick(object? sender, EventArgs e)
    {
        _statusCaretTimer.Stop();

        var document = _pendingCaretDocument;
        _pendingCaretDocument = null;

        if (document is not null && ReferenceEquals(document, Editor.SelectedDocument))
        {
            Status.UpdateCaret(document);
        }
    }

    private void OnStatusContextTimerTick(object? sender, EventArgs e)
    {
        _statusContextTimer.Stop();

        var document = _pendingStatusDocument;
        _pendingStatusDocument = null;

        if (document is not null && ReferenceEquals(document, Editor.SelectedDocument))
        {
            Status.UpdateContext(document);
            UpdatePreviewFile();
        }
    }

    private void OnDocumentTextChanged(object? sender, EditorDocumentViewModel document)
    {
        if (WeftDocumentKinds.IsWeftFile(document.FilePath))
        {
            ScheduleCompile();
        }

        ScheduleStatus(document);
    }

    private void ScheduleStatus(EditorDocumentViewModel document)
    {
        _pendingCaretDocument = document;
        _statusCaretTimer.Stop();
        _statusCaretTimer.Start();
        _pendingStatusDocument = document;
        _statusContextTimer.Stop();
        _statusContextTimer.Start();
    }

    private void ScheduleCompile()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return;
        }

        _analysisScheduler.Schedule(
            _projectFolder,
            reloadFromDisk: false,
            buildRuntime: IsPreviewVisible);
    }
}
