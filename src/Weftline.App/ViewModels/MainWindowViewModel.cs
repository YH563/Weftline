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

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IDialogService _dialogs;
    private readonly EditorLanguageService _editorLanguageService;
    private readonly IProjectAnalysisScheduler _analysisScheduler;
    private readonly IProjectLayoutStore _layoutStore;
    private readonly IDocumentSaver _documentSaver;
    private readonly DocumentSnapshotProvider _snapshots;
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
    private ProjectFolderWatcher? _projectWatcher;
    private bool _disposed;

    public MainWindowViewModel(MainWindowServices services)
    {
        _dialogs = services.Dialogs;
        _analysisScheduler = services.Analysis;
        _layoutStore = services.Layouts;
        _documentSaver = services.Documents;
        _snapshots = services.Snapshots;
        _editorLanguageService = services.Language;

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

        Explorer = services.Explorer;
        Editor = services.Editor;
        Status = new StatusBarViewModel();
        Graph = new GraphViewModel();
        Preview = new PreviewPanelViewModel(NavigateToSource);

        _analysisScheduler.Started += OnAnalysisStarted;
        _analysisScheduler.Completed += OnAnalysisCompleted;
        _analysisScheduler.Failed += OnAnalysisFailed;

        Explorer.FileOpenRequested += OnFileOpenRequested;
        Explorer.FileSystemChanged += OnFileSystemChanged;
        Explorer.DeleteConfirmation = async path =>
        {
            var count = Editor.CountModifiedDocumentsUnder(path);

            if (count == 0)
            {
                return true;
            }

            return await _dialogs
                .ConfirmAsync(
                    "未保存的修改",
                    $"“{Path.GetFileName(path)}”下有 {count} 个未保存文件，删除会丢失这些修改。是否继续？")
                .ConfigureAwait(true);
        };
        Editor.OpenFailed += OnEditorOpenFailed;
        Editor.PropertyChanged += OnEditorPropertyChanged;
        Editor.DocumentTextChanged += OnDocumentTextChanged;
        Editor.Documents.CollectionChanged += OnDocumentsCollectionChanged;
        Editor.CloseConfirmation = document => _dialogs.ConfirmAsync(
            "未保存的修改",
            $"“{document.FileName}”有未保存的修改，关闭后将丢失。是否继续？");
        Status.UpdateDocument(Editor.SelectedDocument);

        _projectFolder = Explorer.RootPath;
        StartWatching(_projectFolder);
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
}
