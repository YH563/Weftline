using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.Editor.Services;
using Weftline.Models;

namespace Weftline.Editor.ViewModels;

public partial class EditorHostViewModel : ObservableObject
{
    private readonly IEditorDocumentLoader _loader;
    private readonly IEditorIconProvider _iconProvider;
    private readonly SyntaxAnalysisScheduler? _syntaxScheduler;
    private readonly Dictionary<string, IReadOnlyList<WeftDiagnostic>> _diagnosticsByFile =
        new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<EditorDocumentViewModel>? DocumentTextChanged;

    /// <summary>
    /// 请求把光标定位到指定文档位置；视图层收到后负责滚动和聚焦。
    /// </summary>
    public event EventHandler<EditorDocumentViewModel>? NavigationRequested;

    [ObservableProperty]
    private EditorDocumentViewModel? _selectedDocument;

    public EditorHostViewModel(
        IEditorDocumentLoader loader,
        IEditorIconProvider iconProvider,
        IEditorThemeSource themeSource,
        IWeftEditorLanguageService? languageService = null)
    {
        _loader = loader;
        _iconProvider = iconProvider;
        ThemeSource = themeSource;
        LanguageService = languageService;
        _syntaxScheduler = languageService is null
            ? null
            : new SyntaxAnalysisScheduler(languageService);
    }

    public IEditorThemeSource ThemeSource { get; }

    public IWeftEditorLanguageService? LanguageService { get; }

    public ObservableCollection<EditorDocumentViewModel> Documents { get; } = [];

    public bool HasDocument => SelectedDocument is not null;

    public void OpenFile(string path)
    {
        var existing = Documents.FirstOrDefault(document =>
            string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            SelectedDocument = existing;
            return;
        }

        var document = new EditorDocumentViewModel(_loader, _iconProvider);
        document.CloseCommand = new RelayCommand(() => CloseDocument(document));

        try
        {
            document.OpenFile(path);
        }
        catch (Exception exception)
        {
            document.FilePath = path;
            document.Title = Path.GetFileName(path);
            document.UpdateText(exception.Message, false);
        }

        if (_diagnosticsByFile.TryGetValue(document.FilePath, out var diagnostics))
        {
            document.SetProjectDiagnostics(diagnostics);
        }

        document.TextChanged += OnDocumentTextChanged;
        Documents.Add(document);
        SelectedDocument = document;
        _syntaxScheduler?.Schedule(document);
    }

    /// <summary>
    /// 用一次项目编译结果更新各文件的项目诊断；未打开的文件会在打开时应用。
    /// 语法诊断由 SyntaxAnalysisScheduler 单独维护，并在文档层合并。
    /// </summary>
    public void SetDiagnostics(IReadOnlyList<WeftDiagnostic> diagnostics)
    {
        _diagnosticsByFile.Clear();

        foreach (var group in diagnostics.GroupBy(
                     static item => item.FilePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            _diagnosticsByFile[group.Key] = group.ToList();
        }

        foreach (var document in Documents)
        {
            var next = _diagnosticsByFile.TryGetValue(
                document.FilePath,
                out var fileDiagnostics)
                ? fileDiagnostics
                : [];

            document.SetProjectDiagnostics(next);
        }
    }

    /// <summary>
    /// 打开文件并请求定位到指定行列。
    /// </summary>
    public void NavigateTo(string path, int line, int column)
    {
        OpenFile(path);

        if (SelectedDocument is not { } document)
        {
            return;
        }

        document.CaretLine = Math.Max(1, line);
        document.CaretColumn = Math.Max(1, column);
        document.SelectionStart = document.GetOffset(document.CaretLine, document.CaretColumn);
        document.SelectionLength = 0;
        NavigationRequested?.Invoke(this, document);
    }

    public void CloseAll()
    {
        _syntaxScheduler?.CancelAll();

        foreach (var document in Documents)
        {
            document.TextChanged -= OnDocumentTextChanged;
        }

        Documents.Clear();
        SelectedDocument = null;
    }

    public void CloseDocumentsUnder(string path)
    {
        foreach (var document in Documents
                     .Where(document => IsSameOrUnder(document.FilePath, path))
                     .ToList())
        {
            CloseDocument(document);
        }
    }

    public void MoveDocuments(string oldPath, string newPath)
    {
        foreach (var document in Documents.ToList())
        {
            if (!IsSameOrUnder(document.FilePath, oldPath))
            {
                continue;
            }

            var relative = Path.GetRelativePath(oldPath, document.FilePath);
            document.FilePath = relative == "."
                ? newPath
                : Path.Combine(newPath, relative);
            _syntaxScheduler?.Schedule(document);
        }
    }

    public void CloseDocument(EditorDocumentViewModel document)
    {
        var index = Documents.IndexOf(document);

        if (index < 0)
        {
            return;
        }

        document.TextChanged -= OnDocumentTextChanged;
        _syntaxScheduler?.Cancel(document);
        Documents.RemoveAt(index);

        if (ReferenceEquals(SelectedDocument, document))
        {
            SelectedDocument = Documents.Count == 0
                ? null
                : Documents[Math.Min(index, Documents.Count - 1)];
        }
    }

    partial void OnSelectedDocumentChanged(EditorDocumentViewModel? value)
        => OnPropertyChanged(nameof(HasDocument));

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (sender is not EditorDocumentViewModel document)
        {
            return;
        }

        _syntaxScheduler?.Schedule(document);
        DocumentTextChanged?.Invoke(this, document);
    }

    private static bool IsSameOrUnder(string path, string root)
        => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
