using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Models;
using Weftline.App.Services;
using Weftline.Editor.ViewModels;
using Weftline.Models;
using Weftline.Native;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly EditorLanguageService _editorLanguageService;
    private readonly SemaphoreSlim _compileGate = new(1, 1);
    private EditorDocumentViewModel? _observedDocument;
    private CancellationTokenSource? _compileCts;
    private NativeWeftlineClient? _nativeClient;
    private GraphProjection? _lastGoodProjection;
    private string? _projectFolder;
    private long _editVersion;

    public MainWindowViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        Explorer = new ProjectExplorerViewModel(dialogs);
        _editorLanguageService = new EditorLanguageService();
        Editor = new EditorHostViewModel(
            new FileEditorDocumentLoader(),
            new EditorIconProvider(),
            new EditorThemeSource(),
            _editorLanguageService);
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

    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = [];

    [ObservableProperty]
    private bool _isProblemsVisible;

    [ObservableProperty]
    private DiagnosticItem? _selectedDiagnostic;

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
        _lastGoodProjection = null;
        _editorLanguageService.Projection = null;
        _editorLanguageService.Symbols = null;
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
    private void ShowProblems() => IsProblemsVisible = !IsProblemsVisible;

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
        if (sender is EditorDocumentViewModel document)
        {
            Status.UpdateDocument(document);
        }
    }

    private void OnDocumentTextChanged(object? sender, EditorDocumentViewModel document)
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

        Status.SetStatus("正在分析项目…");
        await CompileWithNativeAsync(folder, version, cts).ConfigureAwait(true);
    }

    private async Task CompileWithNativeAsync(
        string folder,
        long version,
        CancellationTokenSource cts)
    {
        NativeProjectResponse native;

        try
        {
            await _compileGate.WaitAsync(cts.Token).ConfigureAwait(true);

            try
            {
                native = await Task.Run(
                    () =>
                    {
                        _nativeClient ??= new NativeWeftlineClient();
                        _nativeClient.LoadFolder(folder, CreateDocumentMap());
                        return _nativeClient.Compile(
                            allowPartial: true, buildProjection: true);
                    },
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
                UpdateDiagnostics([]);
                Editor.SetDiagnostics([]);
                _editorLanguageService.Projection = null;
                _editorLanguageService.Symbols = null;
                Status.UpdateProject(null);
                Status.SetStatus($"分析失败：{exception.Message}");
            }

            return;
        }

        if (cts.IsCancellationRequested || version != _editVersion)
        {
            return;
        }

        ApplyNativeResult(native);
    }

    private Dictionary<string, string> CreateDocumentMap()
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var document in Editor.Documents)
        {
            if (!string.IsNullOrWhiteSpace(document.FilePath))
            {
                documents[document.FilePath] = document.Text;
            }
        }

        return documents;
    }

    private void ApplyNativeResult(NativeProjectResponse native)
    {
        var projection = native.Projection?.ToGraphProjection();
        _editorLanguageService.Projection = projection;
        _editorLanguageService.Symbols = native.Symbols?.ToSymbols();
        var errors = native.Diagnostics.Count(static item => item.Severity == "error");
        var warnings = native.Diagnostics.Count(static item => item.Severity == "warning");
        var variableCount = native.Graph?.Globals.Count ?? 0;
        var moduleCount = projection?.Modules.Count ?? 0;

        UpdateDiagnostics(native.Diagnostics.Select(item => CreateDiagnostic(
            item.FilePath,
            item.Start,
            item.Severity,
            item.Code,
            item.Message)));

        Editor.SetDiagnostics(native.Diagnostics.Select(ToWeftDiagnostic).ToList());

        Status.UpdateProject(new ProjectCompileSummary(
            moduleCount, variableCount, errors, warnings));
        Status.SetStatus(errors == 0 ? "就绪" : $"分析完成：{errors} 个错误");

        if (projection is { } available && moduleCount > 0)
        {
            Graph.Projection = available;
            _lastGoodProjection = available;
            Graph.StatusText = $"{moduleCount} 模块 · {errors} 错误 · {warnings} 警告";
            return;
        }

        if (_lastGoodProjection is { } lastProjection)
        {
            Graph.Projection = lastProjection;
            _editorLanguageService.Projection = lastProjection;
            Graph.StatusText = $"保留上一次可用图 · {errors} 错误 · {warnings} 警告";
            return;
        }

        Graph.Projection = projection;
        Graph.StatusText = errors == 0 ? "暂无模块" : $"{errors} 错误 · {warnings} 警告";
    }

    /// <summary>
    /// 打开诊断对应的文件并定位到源码位置。
    /// </summary>
    public void NavigateToDiagnostic(DiagnosticItem diagnostic)
        => Editor.NavigateTo(diagnostic.FilePath, diagnostic.Line, diagnostic.Character);

    private void UpdateDiagnostics(IEnumerable<DiagnosticItem> diagnostics)
    {
        Diagnostics.Clear();

        foreach (var diagnostic in diagnostics)
        {
            Diagnostics.Add(diagnostic);
        }
    }

    private DiagnosticItem CreateDiagnostic(
        string filePath,
        int start,
        string severity,
        string code,
        string message)
    {
        var (line, character) = ResolvePosition(filePath, start);
        return new DiagnosticItem(filePath, line, character, severity, code, message);
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

    private (int Line, int Character) ResolvePosition(string filePath, int offset)
    {
        var text = GetDocumentText(filePath);

        if (text is null)
        {
            return (1, 1);
        }

        offset = Math.Clamp(offset, 0, text.Length);
        var line = 1;
        var lineStart = 0;

        for (var index = 0; index < offset; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart + 1);
    }

    private string? GetDocumentText(string filePath)
    {
        var document = Editor.Documents.FirstOrDefault(candidate =>
            string.Equals(candidate.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (document is not null)
        {
            return document.Text;
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
