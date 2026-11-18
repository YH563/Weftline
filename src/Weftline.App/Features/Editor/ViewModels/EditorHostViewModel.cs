using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Features.Editor.Services;
using Weftline.App.Services;
using Weftline.Core;

namespace Weftline.App.Features.Editor.ViewModels;

public sealed record EditorOpenFailedEventArgs(string FilePath, Exception Error);

public readonly record struct ExternalReloadResult(int Reloaded, int Conflicts);

public partial class EditorHostViewModel : ObservableObject, IDisposable
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

    /// <summary>
    /// 打开文件失败时触发；失败的文件不会加入标签页。
    /// Raised when opening a file fails; the failed file is not added as a tab.
    /// </summary>
    public event EventHandler<EditorOpenFailedEventArgs>? OpenFailed;

    /// <summary>
    /// 关闭有未保存修改的标签页前调用；返回 false 表示取消关闭。
    /// Invoked before closing a modified tab; false cancels the close.
    /// </summary>
    public Func<EditorDocumentViewModel, Task<bool>>? CloseConfirmation { get; set; }

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

    public bool OpenFile(string path)
    {
        var existing = Documents.FirstOrDefault(document =>
            string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            SelectedDocument = existing;
            return true;
        }

        var document = new EditorDocumentViewModel(_loader, _iconProvider);
        document.CloseCommand = new AsyncRelayCommand(() => CloseDocumentAsync(document));

        try
        {
            document.OpenFile(path);
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, $"打开文件失败：{path}");
            OpenFailed?.Invoke(this, new EditorOpenFailedEventArgs(path, exception));
            return false;
        }

        if (_diagnosticsByFile.TryGetValue(document.FilePath, out var diagnostics))
        {
            document.SetProjectDiagnostics(diagnostics);
        }

        document.TextChanged += OnDocumentTextChanged;
        Documents.Add(document);
        SelectedDocument = document;
        _syntaxScheduler?.Schedule(document);
        return true;
    }

    /// <summary>
    /// 外部文件变化后重新加载未修改的已打开文档；未保存文档保留现状并计入冲突。
    /// Reloads unmodified open documents after external changes; modified documents
    /// are left untouched and reported as conflicts.
    /// </summary>
    public int CountModifiedDocumentsUnder(string path)
        => Documents.Count(document =>
            document.IsModified && IsSameOrUnder(document.FilePath, path));

    public ExternalReloadResult ReloadDocumentsFromDisk(
        IReadOnlyCollection<string> changedPaths)
    {
        var reloaded = 0;
        var conflicts = 0;

        foreach (var document in Documents.ToList())
        {
            if (!IsAffected(document.FilePath, changedPaths))
            {
                continue;
            }

            if (document.IsModified)
            {
                conflicts++;
                continue;
            }

            try
            {
                document.ReloadFromDisk(_loader.Read(document.FilePath));
                reloaded++;
            }
            catch (FileNotFoundException)
            {
                CloseDocument(document);
                reloaded++;
            }
            catch (DirectoryNotFoundException)
            {
                CloseDocument(document);
                reloaded++;
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, $"重新读取文件失败：{document.FilePath}");
            }
        }

        return new ExternalReloadResult(reloaded, conflicts);
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
        if (!OpenFile(path) || SelectedDocument is not { } document)
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

    public void Dispose()
    {
        _syntaxScheduler?.Dispose();
        CloseAll();
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

    public async Task<bool> CloseDocumentAsync(EditorDocumentViewModel document)
    {
        if (document.IsModified && CloseConfirmation is { } confirmation)
        {
            try
            {
                if (!await confirmation(document).ConfigureAwait(true))
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                AppLog.Error(exception, $"确认关闭失败：{document.FilePath}");
                return false;
            }
        }

        CloseDocument(document);
        return true;
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

    private static bool IsAffected(
        string filePath,
        IReadOnlyCollection<string> changedPaths)
    {
        foreach (var changedPath in changedPaths)
        {
            if (IsSameOrUnder(filePath, changedPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrUnder(string path, string root)
        => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
