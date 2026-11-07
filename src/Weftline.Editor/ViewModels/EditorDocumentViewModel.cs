using System.Text.RegularExpressions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.Editor.Services;
using Weftline.Models;

namespace Weftline.Editor.ViewModels;

public partial class EditorDocumentViewModel : ObservableObject
{
    private static readonly Regex TitleRegex = new(
        @"^\s*@title\s+(?<title>.+?)\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private readonly IEditorDocumentLoader _loader;
    private readonly IEditorIconProvider _iconProvider;

    [ObservableProperty]
    private string _title = "欢迎使用 Weftline";

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _text = """
    @title 欢迎使用 Weftline

    @module 开始
        [双击左侧的文件开始编辑。]
    @next -> END
    """;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    [ObservableProperty]
    private int _selectionLength;

    [ObservableProperty]
    private Geometry _iconGeometry;

    [ObservableProperty]
    private IBrush _iconBrush;

    [ObservableProperty]
    private IReadOnlyList<WeftDiagnostic> _diagnostics = [];

    public EditorDocumentViewModel(IEditorDocumentLoader loader, IEditorIconProvider iconProvider)
    {
        _loader = loader;
        _iconProvider = iconProvider;

        var icon = iconProvider.Resolve("welcome.weft", false, false);
        _iconGeometry = icon.Geometry;
        _iconBrush = icon.Brush;
    }

    public IRelayCommand? CloseCommand { get; set; }

    public string FileName => string.IsNullOrEmpty(FilePath) ? Title : Path.GetFileName(FilePath);

    public void OpenFile(string path)
    {
        var text = _loader.ReadAllText(path);
        FilePath = path;
        Text = text;
        Title = ParseTitle(text, Path.GetFileName(path));
        IsModified = false;
        CaretLine = 1;
        CaretColumn = 1;
    }

    public void UpdateText(string text, bool markModified)
    {
        if (string.Equals(Text, text, StringComparison.Ordinal))
        {
            return;
        }

        Text = text;

        if (markModified)
        {
            IsModified = true;
        }

        if (!string.IsNullOrEmpty(FilePath))
        {
            Title = ParseTitle(text, Path.GetFileName(FilePath));
        }
    }

    partial void OnFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(FileName));
        UpdateIcon();
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(FileName));

    private void UpdateIcon()
    {
        var icon = _iconProvider.Resolve(
            string.IsNullOrEmpty(FilePath) ? "welcome.weft" : FilePath,
            false,
            false);
        IconGeometry = icon.Geometry;
        IconBrush = icon.Brush;
    }

    private static string ParseTitle(string text, string fallback)
    {
        var match = TitleRegex.Match(text);
        return match.Success ? match.Groups["title"].Value.Trim() : fallback;
    }
}
