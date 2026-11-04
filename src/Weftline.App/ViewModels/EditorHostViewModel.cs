using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Weftline.App.ViewModels;

public partial class EditorHostViewModel : ObservableObject
{
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

        Documents.Add(document);
        SelectedDocument = document;
    }

    public void CloseAll()
    {
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

    private static bool IsSameOrUnder(string path, string root)
        => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
