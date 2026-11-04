using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Models;
using Weftline.App.Services;
using Weftline.Language.Projects;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private EditorViewModel? _observedDocument;

    public MainWindowViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        Explorer = new ProjectExplorerViewModel(dialogs);
        Editor = new EditorHostViewModel();
        Status = new StatusBarViewModel();

        Explorer.FileOpenRequested += OnFileOpenRequested;
        Explorer.FileSystemChanged += OnFileSystemChanged;
        Editor.PropertyChanged += OnEditorPropertyChanged;
        Status.UpdateDocument(Editor.SelectedDocument);
        _ = RefreshProjectAsync(Explorer.RootPath);
    }

    public ProjectExplorerViewModel Explorer { get; }

    public EditorHostViewModel Editor { get; }

    public StatusBarViewModel Status { get; }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var folder = await _dialogs.PickFolderAsync("打开项目文件夹").ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Editor.CloseAll();
        Explorer.Load(folder);
        await RefreshProjectAsync(folder).ConfigureAwait(true);
        Status.SetStatus($"已打开：{GetFolderName(folder)}");
    }

    [RelayCommand]
    private void NewProject() => Status.SetStatus("新建项目待实现");

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Editor.SelectedDocument is not { } document)
        {
            Status.SetStatus("没有可保存的文件");
            return;
        }

        try
        {
            await File.WriteAllTextAsync(document.FilePath, document.Text).ConfigureAwait(true);
            document.IsModified = false;
            Status.SetStatus($"已保存：{Path.GetFileName(document.FilePath)}");
            await RefreshProjectAsync(Explorer.RootPath).ConfigureAwait(true);
            Status.SetStatus($"已保存：{Path.GetFileName(document.FilePath)}");
        }
        catch (Exception exception)
        {
            Status.SetStatus($"保存失败：{exception.Message}");
        }
    }

    [RelayCommand]
    private void SaveLayout() => Status.SetStatus("布局保存待实现");

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private void Undo() => Status.SetStatus("撤销：请使用 Ctrl+Z");

    [RelayCommand]
    private void Redo() => Status.SetStatus("重做：请使用 Ctrl+Y");

    [RelayCommand]
    private void ShowProblems() => Status.SetStatus("问题面板待接入");

    [RelayCommand]
    private void ShowOutput() => Status.SetStatus("输出面板待接入");

    [RelayCommand]
    private void ShowProjectInfo() => Status.SetStatus("项目总览待接入");

    [RelayCommand]
    private async Task ValidateAsync()
    {
        await RefreshProjectAsync(Explorer.RootPath).ConfigureAwait(true);
        Status.SetStatus("校验完成");
    }

    [RelayCommand]
    private void Export() => Status.SetStatus("HTML 导出待接入");

    [RelayCommand]
    private void About() => Status.SetStatus("Weftline · 剧情 demo / 叙事演示工具");

    private void OnFileOpenRequested(object? sender, string path) => Editor.OpenFile(path);

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
        await RefreshProjectAsync(Explorer.RootPath).ConfigureAwait(true);
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

        AttachDocument(Editor.SelectedDocument);
        Status.UpdateDocument(Editor.SelectedDocument);
    }

    private void AttachDocument(EditorViewModel? document)
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
        if (sender is EditorViewModel document)
        {
            Status.UpdateDocument(document);
        }
    }

    private async Task<ProjectCompilationResult?> RefreshProjectAsync(string folder)
    {
        Status.SetStatus("正在分析项目…");

        try
        {
            var result = await Task.Run(() => ProjectCompiler.Compile(folder)).ConfigureAwait(true);
            Status.UpdateProject(result);
            Status.SetStatus("就绪");
            return result;
        }
        catch (Exception exception)
        {
            Status.UpdateProject(null);
            Status.SetStatus($"分析失败：{exception.Message}");
            return null;
        }
    }

    private static string GetFolderName(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? folder : name;
    }
}
