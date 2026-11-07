using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
    private readonly DispatcherTimer _statusContextTimer;
    private readonly DispatcherTimer _statusCaretTimer;
    private EditorDocumentViewModel? _observedDocument;
    private EditorDocumentViewModel? _pendingStatusDocument;
    private EditorDocumentViewModel? _pendingCaretDocument;
    private CancellationTokenSource? _compileCts;
    private NativeWeftlineClient? _nativeClient;
    private GraphProjection? _lastGoodProjection;
    private string? _lastGraphSignature;
    private string? _projectFolder;
    private long _editVersion;

    public MainWindowViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        _statusContextTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _statusContextTimer.Tick += OnStatusContextTimerTick;

        _statusCaretTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _statusCaretTimer.Tick += OnStatusCaretTimerTick;

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

        await OpenProjectCoreAsync(folder).ConfigureAwait(true);
    }

    public async Task LoadProjectForPreviewAsync(string folder, string? file)
    {
        await OpenProjectCoreAsync(folder).ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(file))
        {
            Editor.OpenFile(file);
        }
    }

    private async Task OpenProjectCoreAsync(string folder)
    {
        Editor.CloseAll();
        Explorer.Load(folder);
        _projectFolder = folder;
        _lastGoodProjection = null;
        _lastGraphSignature = null;
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

        _statusContextTimer.Stop();
        _statusCaretTimer.Stop();
        _pendingStatusDocument = null;
        _pendingCaretDocument = null;
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
        if (sender is not EditorDocumentViewModel document)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(EditorDocumentViewModel.CaretLine):
            case nameof(EditorDocumentViewModel.CaretColumn):
            case nameof(EditorDocumentViewModel.SelectionLength):
            case nameof(EditorDocumentViewModel.Text):
                _pendingCaretDocument = document;
                _statusCaretTimer.Stop();
                _statusCaretTimer.Start();
                _pendingStatusDocument = document;
                _statusContextTimer.Stop();
                _statusContextTimer.Start();
                break;
            case nameof(EditorDocumentViewModel.FilePath):
                Status.UpdateDocument(document);
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
            await CompileCoreAsync(version, cts, reloadFromDisk: false).ConfigureAwait(true);
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
        await CompileCoreAsync(version, cts, reloadFromDisk: true).ConfigureAwait(true);
    }

    private async Task CompileCoreAsync(
        long version,
        CancellationTokenSource cts,
        bool reloadFromDisk)
    {
        var folder = _projectFolder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Status.SetStatus("正在分析项目…");
        await CompileWithNativeAsync(folder, version, cts, reloadFromDisk).ConfigureAwait(true);
    }

    private async Task CompileWithNativeAsync(
        string folder,
        long version,
        CancellationTokenSource cts,
        bool reloadFromDisk)
    {
        CompileOutput output;

        // UI 线程先抓取文档快照，避免后台线程遍历 ObservableCollection。
        var documents = CreateDocumentMap();

        try
        {
            await _compileGate.WaitAsync(cts.Token).ConfigureAwait(true);

            try
            {
                cts.Token.ThrowIfCancellationRequested();

                output = await Task.Run(
                    () =>
                    {
                        _nativeClient ??= new NativeWeftlineClient();
                        var projectDocuments = _nativeClient.LoadFolder(
                            folder, documents, reloadFromDisk);
                        cts.Token.ThrowIfCancellationRequested();

                        var native = _nativeClient.Compile(
                            allowPartial: true, buildProjection: true);

                        return new CompileOutput(
                            native,
                            native.Projection?.ToGraphProjection(),
                            native.Symbols?.ToSymbols(),
                            projectDocuments);
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

        ApplyNativeResult(output);
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

    private sealed record CompileOutput(
        NativeProjectResponse Native,
        GraphProjection? Projection,
        WeftProjectSymbols? Symbols,
        IReadOnlyDictionary<string, string> Documents);

    private void ApplyNativeResult(CompileOutput output)
    {
        var native = output.Native;
        var projection = output.Projection;
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
        var signature = projection is null ? null : BuildGraphSignature(projection);

        if (string.Equals(signature, _lastGraphSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastGraphSignature = signature;
        Graph.Projection = projection;
    }

    private static string BuildGraphSignature(GraphProjection projection)
    {
        var builder = new StringBuilder();
        builder.Append(projection.EntryModuleId).Append('|');

        foreach (var chapter in projection.Chapters)
        {
            builder.Append(chapter.ChapterId).Append('~')
                .Append(chapter.Title).Append('~')
                .Append((int)chapter.Flags).Append(':');

            foreach (var moduleId in chapter.ModuleIds)
            {
                builder.Append(moduleId).Append(',');
            }

            builder.Append('|');
        }

        foreach (var module in projection.Modules)
        {
            builder.Append(module.ModuleId).Append('~')
                .Append((int)module.Flags).Append('|');
        }

        foreach (var edge in projection.ModuleEdges)
        {
            builder.Append(edge.FromModule).Append('>').Append(edge.ToModule).Append('|');
        }

        foreach (var edge in projection.ChapterEdges)
        {
            builder.Append(edge.FromChapter).Append('>').Append(edge.ToChapter).Append('|');
        }

        return builder.ToString();
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
