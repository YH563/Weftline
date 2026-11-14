using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Models;
using Weftline.App.Services;
using Weftline.Editor.Services;
using Weftline.Editor.ViewModels;
using Weftline.Graph.Layout;
using Weftline.Models;
using Weftline.Native;
using Weftline.Player;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private const string LayoutFileName = "weftline.layout.json";

    private static readonly JsonSerializerOptions LayoutJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IDialogService _dialogs;
    private readonly EditorLanguageService _editorLanguageService;
    private readonly ProjectAnalysisScheduler _analysisScheduler;
    private readonly Dictionary<string, DocumentSnapshotEntry> _documentSnapshots =
        new(StringComparer.Ordinal);
    private readonly DispatcherTimer _statusContextTimer;
    private readonly DispatcherTimer _statusCaretTimer;
    private EditorDocumentViewModel? _observedDocument;
    private EditorDocumentViewModel? _pendingStatusDocument;
    private EditorDocumentViewModel? _pendingCaretDocument;
    private GraphProjection? _lastGoodProjection;
    private RuntimeProjection? _lastRuntime;
    private WeftProjectSymbols? _lastSymbols;
    private string? _lastGraphSignature;
    private string? _projectFolder;

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
        Preview = new PreviewPanelViewModel(NavigateToSource);

        _analysisScheduler = new ProjectAnalysisScheduler(CreateDocumentMap);
        _analysisScheduler.Started += OnAnalysisStarted;
        _analysisScheduler.Completed += OnAnalysisCompleted;
        _analysisScheduler.Failed += OnAnalysisFailed;

        Explorer.FileOpenRequested += OnFileOpenRequested;
        Explorer.FileSystemChanged += OnFileSystemChanged;
        Editor.PropertyChanged += OnEditorPropertyChanged;
        Editor.DocumentTextChanged += OnDocumentTextChanged;
        Editor.Documents.CollectionChanged += OnDocumentsCollectionChanged;
        Status.UpdateDocument(Editor.SelectedDocument);

        _projectFolder = Explorer.RootPath;
        UpdatePreviewFile();
        _ = CompileNowAsync();
    }

    public ProjectExplorerViewModel Explorer { get; }

    public EditorHostViewModel Editor { get; }

    public StatusBarViewModel Status { get; }

    public GraphViewModel Graph { get; }

    public PreviewPanelViewModel Preview { get; }

    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = [];

    [ObservableProperty]
    private bool _isGraphVisible = true;

    [ObservableProperty]
    private bool _isPreviewVisible = true;

    [ObservableProperty]
    private bool _isProblemsVisible;

    [ObservableProperty]
    private DiagnosticItem? _selectedDiagnostic;

    public event EventHandler? ProjectOpened;

    public event EventHandler? LayoutSaveRequested;

    public event EventHandler<PreviewRequestedEventArgs>? PreviewRequested;

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
            Editor.OpenFile(ResolvePreviewFile(folder, file));
        }
    }

    /// <summary>
    /// 把 --file 解析成绝对路径。相对路径按项目目录解析，找不到时回退到当前工作目录。
    /// 必须解析成绝对路径：否则同一个文件会以两个 key 登记两次，
    /// 让 native core 把每个声明都判成重复。
    /// </summary>
    private static string ResolvePreviewFile(string folder, string file)
    {
        if (Path.IsPathRooted(file))
        {
            return Path.GetFullPath(file);
        }

        var inProject = Path.GetFullPath(Path.Combine(folder, file));
        return File.Exists(inProject) || !File.Exists(file)
            ? inProject
            : Path.GetFullPath(file);
    }

    private async Task OpenProjectCoreAsync(string folder)
    {
        _analysisScheduler.Cancel();
        Editor.CloseAll();
        _documentSnapshots.Clear();
        Explorer.Load(folder);
        _projectFolder = folder;
        _lastGoodProjection = null;
        _lastRuntime = null;
        _lastSymbols = null;
        _lastGraphSignature = null;
        _editorLanguageService.Projection = null;
        _editorLanguageService.Symbols = null;
        Graph.Reset();
        Preview.Reset();
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
            await File.WriteAllTextAsync(document.FilePath, document.GetTextSnapshot()).ConfigureAwait(true);
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
    private void SaveLayout() => LayoutSaveRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 把视图导出的布局快照写入当前项目的侧车文件。
    /// Writes a layout snapshot exported by the view to the current project sidecar.
    /// </summary>
    public void SaveLayout(GraphLayoutSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.SetStatus("没有打开项目");
            return;
        }

        try
        {
            var path = Path.Combine(_projectFolder, LayoutFileName);
            var document = ProjectLayoutDocument.FromSnapshot(snapshot);
            File.WriteAllText(path, JsonSerializer.Serialize(document, LayoutJsonOptions));
            Status.SetStatus("已保存布局");
        }
        catch (Exception exception)
        {
            Status.SetStatus($"保存布局失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 读取当前项目的布局侧车；不存在或损坏时返回 null。
    /// Loads the current project layout sidecar; returns null when missing or invalid.
    /// </summary>
    public GraphLayoutSnapshot? LoadLayout()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return null;
        }

        var path = Path.Combine(_projectFolder, LayoutFileName);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<ProjectLayoutDocument>(File.ReadAllText(path));
            return document?.ToSnapshot();
        }
        catch (Exception exception)
        {
            Status.SetStatus($"布局加载失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 删除当前项目的布局侧车，配合「重置布局」。
    /// Deletes the current project layout sidecar, used by Reset Layout.
    /// </summary>
    public void ClearSavedLayout()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return;
        }

        try
        {
            var path = Path.Combine(_projectFolder, LayoutFileName);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            Status.SetStatus("布局已重置");
        }
        catch (Exception exception)
        {
            Status.SetStatus($"清除布局失败：{exception.Message}");
        }
    }

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
    private Task PreviewAsync() => OpenPreviewAsync(startAtCurrentModule: true);

    [RelayCommand]
    private Task PreviewFromStartAsync() => OpenPreviewAsync(startAtCurrentModule: false);

    private async Task OpenPreviewAsync(bool startAtCurrentModule)
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.SetStatus("没有打开项目");
            return;
        }

        Status.SetStatus("正在准备预览…");

        try
        {
            await CompileNowAsync().ConfigureAwait(true);
            var projection = await _analysisScheduler
                .CompileRuntimeAsync()
                .ConfigureAwait(true);

            var startModule = startAtCurrentModule
                ? ResolveCurrentModuleId()
                : projection.Entry;

            var viewModel = new PreviewViewModel(
                projection,
                _projectFolder,
                startModule);

            PreviewRequested?.Invoke(this, new PreviewRequestedEventArgs(viewModel));
            Status.SetStatus("预览已打开");
        }
        catch (Exception exception)
        {
            Status.SetStatus($"预览失败：{exception.Message}");
            await _dialogs
                .ShowMessageAsync("无法预览", exception.Message)
                .ConfigureAwait(true);
        }
    }

    /// <summary>
    /// 取光标所在模块，供“从当前模块预览”使用；.wstory 或模块之前返回 null。
    /// </summary>
    private string? ResolveCurrentModuleId()
    {
        if (Editor.SelectedDocument is not { } document)
        {
            return null;
        }

        if (!document.FilePath.EndsWith(".weft", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var textDocument = document.Document;

        if (textDocument.TextLength == 0)
        {
            return null;
        }

        var limit = Math.Clamp(document.CaretLine, 1, textDocument.LineCount);
        string? module = null;

        for (var lineNumber = 1; lineNumber <= limit; lineNumber++)
        {
            var line = textDocument
                .GetText(textDocument.GetLineByNumber(lineNumber))
                .Trim();

            if (line.StartsWith("@module ", StringComparison.Ordinal))
            {
                module = line["@module ".Length..].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(module) ? null : module;
    }

    [RelayCommand]
    private void About() => Status.SetStatus("Weftline · 剧情 demo / 叙事演示工具");

    private void OnFileOpenRequested(object? sender, string path) => Editor.OpenFile(path);

    private void OnDocumentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _documentSnapshots.Clear();

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

        Preview.UpdateProject(
            _projectFolder ?? string.Empty,
            output.Runtime ?? _lastRuntime,
            projection ?? _lastGoodProjection,
            isStale: output.Runtime is null && errors > 0);
        UpdatePreviewFile();

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
                .Append((int)module.Flags).Append('~')
                .Append(string.Join(',', module.Scenes)).Append('~')
                .Append(module.Summary.FirstLine).Append('~')
                .Append(module.Summary.DialogueCount).Append('~')
                .Append(module.Summary.ChoiceCount).Append('~');
            AppendSource(builder, module.Source);
            builder.Append('|');
        }

        foreach (var edge in projection.ModuleEdges)
        {
            builder.Append(edge.FromModule).Append('>').Append(edge.ToModule).Append('~')
                .Append((int)edge.Mode).Append('~')
                .Append(edge.Label).Append('~')
                .Append(edge.Condition).Append('~');
            AppendSource(builder, edge.Source);
            builder.Append('|');
        }

        foreach (var edge in projection.ChapterEdges)
        {
            builder.Append(edge.FromChapter).Append('>').Append(edge.ToChapter).Append(':');

            foreach (var detail in edge.Details)
            {
                builder.Append(detail.FromModule).Append('>').Append(detail.ToModule).Append('~')
                    .Append((int)detail.Mode).Append('~')
                    .Append(detail.Label).Append('~')
                    .Append(detail.Condition).Append('~');
                AppendSource(builder, detail.Source);
                builder.Append(',');
            }

            builder.Append('|');
        }

        AppendReferenceMap(builder, 'c', projection.References.Characters);
        AppendReferenceMap(builder, 'i', projection.References.Items);
        AppendReferenceMap(builder, 's', projection.References.Scenes);
        AppendReferenceMap(builder, 'v', projection.References.Variables);

        return builder.ToString();
    }

    private static void AppendReferenceMap(
        StringBuilder builder,
        char kind,
        IReadOnlyDictionary<string, IReadOnlyList<string>> references)
    {
        builder.Append(kind).Append(':');

        foreach (var (name, modules) in references.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            builder.Append(name).Append('=');

            foreach (var moduleId in modules)
            {
                builder.Append(moduleId).Append(',');
            }

            builder.Append(';');
        }

        builder.Append('|');
    }

    private static void AppendSource(StringBuilder builder, SourceLocation source)
        => builder.Append(source.FilePath).Append('@')
            .Append(source.Span.Start).Append(':')
            .Append(source.Span.Length);

    /// <summary>
    /// 打开诊断对应的文件并定位到源码位置。
    /// </summary>
    public void NavigateToDiagnostic(DiagnosticItem diagnostic)
        => Editor.NavigateTo(diagnostic.FilePath, diagnostic.Line, diagnostic.Character);

    /// <summary>
    /// 双击模块：打开所属 .weft 并定位到 @module。
    /// Double-clicking a module opens its .weft file at the module declaration.
    /// </summary>
    public void ActivateModule(GraphModuleNode module)
        => NavigateToSource(module.Source, $"模块 {module.ModuleId}");

    /// <summary>
    /// 双击模块边：定位到对应分支行。
    /// Double-clicking a module edge navigates to the branch line.
    /// </summary>
    public void ActivateModuleEdge(GraphModuleEdge edge)
        => NavigateToSource(edge.Source, $"{edge.FromModule} → {edge.ToModule}");

    /// <summary>
    /// 双击章节边：展示底层模块对明细。
    /// Double-clicking a chapter edge shows its underlying module branches.
    /// </summary>
    public void ActivateChapterEdge(ChapterEdge edge)
    {
        Graph.ShowChapterEdgeDetails(
            $"{edge.FromChapter} → {edge.ToChapter}",
            $"{edge.Count} 条模块分支",
            edge.Details);
        Status.SetStatus($"章节边：{edge.FromChapter} → {edge.ToChapter}");
    }

    /// <summary>
    /// 悬停模块时更新摘要；离开模块时隐藏。
    /// Updates the hover summary for a module; hides it when leaving modules.
    /// </summary>
    public void UpdateModuleHover(GraphModuleNode? module)
    {
        if (module is null)
        {
            Graph.ClearHover();
            return;
        }

        var chapter = _lastSymbols?.Chapters.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, module.ChapterId, StringComparison.Ordinal));

        var meta = new List<string>();

        if (module.Scenes.Count > 0)
        {
            meta.Add($"场景 {string.Join(" / ", module.Scenes)}");
        }

        meta.Add($"{module.Summary.DialogueCount} 句对话");
        meta.Add($"{module.Summary.ChoiceCount} 个选项");

        Graph.ShowHover(
            module.ModuleId,
            module.Summary.FirstLine,
            string.Join(" · ", meta),
            chapter?.Description);
    }

    private void NavigateToSource(SourceLocation location, string label)
    {
        if (string.IsNullOrWhiteSpace(location.FilePath))
        {
            return;
        }

        var (line, character) = (1, 1);
        var text = GetDocumentText(location.FilePath);

        if (text is not null)
        {
            (line, character) = new LineIndex(text).Resolve(location.Span.Start);
        }

        Editor.NavigateTo(location.FilePath, line, character);
        Status.SetStatus($"已定位：{label}");
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
    [RelayCommand]
    private void TogglePreviewPanel() => IsPreviewVisible = !IsPreviewVisible;

    partial void OnIsGraphVisibleChanged(bool value)
    {
        if (!value && !IsPreviewVisible)
        {
            IsPreviewVisible = true;
        }
    }

    partial void OnIsPreviewVisibleChanged(bool value)
    {
        if (!value && !IsGraphVisible)
        {
            IsGraphVisible = true;
        }

        if (value)
        {
            _ = EnsurePreviewAsync();
        }
        else
        {
            UpdatePreviewFile();
        }
    }

    public async Task EnsurePreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return;
        }

        if (_lastRuntime is not null)
        {
            Preview.UpdateProject(
                _projectFolder,
                _lastRuntime,
                _lastGoodProjection,
                isStale: false);
            UpdatePreviewFile();
        }

        await CompileNowAsync(includeRuntime: true).ConfigureAwait(true);
    }

    private void UpdatePreviewFile()
        => Preview.SetFile(Editor.SelectedDocument?.FilePath);

}
