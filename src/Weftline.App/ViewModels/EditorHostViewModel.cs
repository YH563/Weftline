using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Weftline.App.ViewModels;

public partial class EditorHostViewModel : ObservableObject
{
    public event EventHandler<EditorViewModel>? DocumentTextChanged;

    /// <summary>
    /// 请求把光标定位到指定文档位置；视图层收到后负责滚动和聚焦。
    /// </summary>
    public event EventHandler<EditorViewModel>? NavigationRequested;

    [ObservableProperty]
    private EditorViewModel? _selectedDocument;

    public EditorHostViewModel()
    {
    }

    public ObservableCollection<EditorViewModel> Documents { get; } = [];

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

        var document = new EditorViewModel();
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

        document.PropertyChanged += OnDocumentPropertyChanged;
        Documents.Add(document);
        SelectedDocument = document;
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

    public void CloseDocument(EditorViewModel document)
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

    partial void OnSelectedDocumentChanged(EditorViewModel? value)
        => OnPropertyChanged(nameof(HasDocument));

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.Text) &&
            sender is EditorViewModel document)
        {
            DocumentTextChanged?.Invoke(this, document);
        }
    }

    private static bool IsSameOrUnder(string path, string root)
        => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
