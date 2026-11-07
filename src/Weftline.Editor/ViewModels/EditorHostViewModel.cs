using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.Editor.Services;
using Weftline.Models;

namespace Weftline.Editor.ViewModels;

public partial class EditorHostViewModel : ObservableObject
{
    private readonly IEditorDocumentLoader _loader;
    private readonly IEditorIconProvider _iconProvider;
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
            document.Diagnostics = diagnostics;
        }

        document.PropertyChanged += OnDocumentPropertyChanged;
        Documents.Add(document);
        SelectedDocument = document;
    }

    /// <summary>
    /// 用一次编译结果替换所有文件的诊断；未打开的文件会在打开时应用。
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
            document.Diagnostics = _diagnosticsByFile.TryGetValue(
                document.FilePath,
                out var fileDiagnostics)
                ? fileDiagnostics
                : [];
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
        NavigationRequested?.Invoke(this, document);
    }

    public void CloseAll()
    {
        foreach (var document in Documents)
        {
            document.PropertyChanged -= OnDocumentPropertyChanged;
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
        }
    }

    public void CloseDocument(EditorDocumentViewModel document)
    {
        var index = Documents.IndexOf(document);

        if (index < 0)
        {
            return;
        }

        document.PropertyChanged -= OnDocumentPropertyChanged;
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

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorDocumentViewModel.Text) &&
            sender is EditorDocumentViewModel document)
        {
            DocumentTextChanged?.Invoke(this, document);
        }
    }

    private static bool IsSameOrUnder(string path, string root)
        => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
