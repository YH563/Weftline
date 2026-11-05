using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Models;
using Weftline.App.Services;
using Weftline.Language.Diagnostics;
using Weftline.Language.Projects;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly CompilationCache _compilationCache = new();
    private readonly SemaphoreSlim _compileGate = new(1, 1);
    private EditorViewModel? _observedDocument;
    private CancellationTokenSource? _compileCts;
    private ProjectCompilationResult? _lastGoodResult;
    private string? _projectFolder;
    private long _editVersion;

    public MainWindowViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        Explorer = new ProjectExplorerViewModel(dialogs);
        Editor = new EditorHostViewModel();
        Status = new StatusBarViewModel();
        Graph = new GraphViewModel();

        Explorer.FileOpenRequested += OnFileOpenRequested;
        Explorer.FileSystemChanged += OnFileSystemChanged;
        Editor.PropertyChanged += OnEditorPropertyChanged;
        Editor.DocumentTextChanged += OnDocumentTextChanged;
        Editor.Documents.CollectionChanged += (_, _) => ScheduleCompile();
        Status.UpdateDocument(Editor.SelectedDocument);

        _projectFolder = Explorer.RootPath;
        _ = CompileNowAsync();
    }

    public ProjectExplorerViewModel Explorer { get; }

    public EditorHostViewModel Editor { get; }

    public StatusBarViewModel Status { get; }

    public GraphViewModel Graph { get; }

    public event EventHandler? ProjectOpened;

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
        _projectFolder = folder;
        _compilationCache.Clear();
        _lastGoodResult = null;
        Graph.Reset();
        ProjectOpened?.Invoke(this, EventArgs.Empty);
        await CompileNowAsync().ConfigureAwait(true);
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
            await CompileNowAsync().ConfigureAwait(true);
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
        await CompileNowAsync().ConfigureAwait(true);
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

    private void OnDocumentTextChanged(object? sender, EditorViewModel document)
        => ScheduleCompile();

    private void ScheduleCompile()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return;
        }

        _compileCts?.Cancel();

        var cts = new CancellationTokenSource();
        _compileCts = cts;
        var version = Interlocked.Increment(ref _editVersion);
        _ = CompileAfterDelayAsync(version, cts);
    }

    private async Task CompileAfterDelayAsync(long version, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(150, cts.Token).ConfigureAwait(true);
            await CompileCoreAsync(version, cts).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CompileNowAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.UpdateProject(null);
            return;
        }

        _compileCts?.Cancel();

        var cts = new CancellationTokenSource();
        _compileCts = cts;
        var version = Interlocked.Increment(ref _editVersion);
        await CompileCoreAsync(version, cts).ConfigureAwait(true);
    }

    private async Task CompileCoreAsync(long version, CancellationTokenSource cts)
    {
        var folder = _projectFolder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var snapshot = CreateSnapshot(folder, version);
        Status.SetStatus("正在分析项目…");

        ProjectCompilationResult result;

        try
        {
            await _compileGate.WaitAsync(cts.Token).ConfigureAwait(true);

            try
            {
                result = await Task.Run(
                    () => ProjectCompiler.Compile(
                        snapshot,
                        _compilationCache,
                        new CompilationOptions(AllowPartial: true, BuildProjection: true)),
                    cts.Token).ConfigureAwait(true);
            }
            finally
            {
                _compileGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (!cts.IsCancellationRequested)
            {
                Status.UpdateProject(null);
                Status.SetStatus($"分析失败：{exception.Message}");
            }

            return;
        }

        if (cts.IsCancellationRequested || version != _editVersion)
        {
            return;
        }

        ApplyCompileResult(result);
    }

    private ProjectSnapshot CreateSnapshot(string folder, long version)
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var document in Editor.Documents)
        {
            if (!string.IsNullOrWhiteSpace(document.FilePath))
            {
                documents[document.FilePath] = document.Text;
            }
        }

        return new ProjectSnapshot(folder, version, documents);
    }

    private void ApplyCompileResult(ProjectCompilationResult result)
    {
        Status.UpdateProject(result);

        var errors = result.Diagnostics.Count(
            static item => item.Diagnostic.Severity == DiagnosticSeverity.Error);

        var warnings = result.Diagnostics.Count(
            static item => item.Diagnostic.Severity == DiagnosticSeverity.Warning);

        Status.SetStatus(errors == 0 ? "就绪" : $"分析完成：{errors} 个错误");

        var moduleCount = result.Graph?.Modules.Count ?? 0;

        if (result.Projection is { } projection && moduleCount > 0)
        {
            Graph.Projection = projection;
            _lastGoodResult = result;
            Graph.StatusText = $"{moduleCount} 模块 · {errors} 错误 · {warnings} 警告";
            return;
        }

        if (_lastGoodResult?.Projection is { } lastProjection)
        {
            Graph.Projection = lastProjection;
            Graph.StatusText = $"保留上一次可用图 · {errors} 错误 · {warnings} 警告";
            return;
        }

        Graph.Projection = result.Projection;
        Graph.StatusText = errors == 0 ? "暂无模块" : $"{errors} 错误 · {warnings} 警告";
    }

    private static string GetFolderName(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? folder : name;
    }
}
