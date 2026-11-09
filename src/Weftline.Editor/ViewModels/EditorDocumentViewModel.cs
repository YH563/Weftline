using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
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

    private const string WelcomeText = """
    @title 欢迎使用 Weftline

    @module 开始
        [双击左侧的文件开始编辑。]
    @next -> END
    """;

    private readonly IEditorDocumentLoader _loader;
    private readonly IEditorIconProvider _iconProvider;
    private IReadOnlyList<WeftDiagnostic> _syntaxDiagnostics = [];
    private IReadOnlyList<WeftDiagnostic> _projectDiagnostics = [];
    private string? _lastFirstLine;
    private int _firstLineEnd;
    private bool _suppressDocumentChange;

    [ObservableProperty]
    private string _title = "欢迎使用 Weftline";

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    [ObservableProperty]
    private int _selectionStart;

    [ObservableProperty]
    private int _selectionLength;

    [ObservableProperty]
    private double _scrollOffsetX;

    [ObservableProperty]
    private double _scrollOffsetY;

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

        Document = new TextDocument(WelcomeText);
        Document.Changed += OnDocumentChanged;
        RefreshFirstLine();

        var icon = iconProvider.Resolve("welcome.weft", false, false);
        _iconGeometry = icon.Geometry;
        _iconBrush = icon.Brush;
    }

    public TextDocument Document { get; private set; }

    public long Version { get; private set; }

    public event EventHandler? TextChanged;

    public IRelayCommand? CloseCommand { get; set; }

    public string FileName => string.IsNullOrEmpty(FilePath) ? Title : Path.GetFileName(FilePath);

    public void OpenFile(string path)
    {
        var text = _loader.ReadAllText(path);
        FilePath = path;
        ReplaceDocument(text);
        IsModified = false;
        CaretLine = 1;
        CaretColumn = 1;
        SelectionStart = 0;
        SelectionLength = 0;
        ScrollOffsetX = 0;
        ScrollOffsetY = 0;
    }

    public void UpdateText(string text, bool markModified)
    {
        if (string.Equals(Document.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        _suppressDocumentChange = true;
        Document.Text = text;
        _suppressDocumentChange = false;

        if (markModified)
        {
            IsModified = true;
        }

        RefreshFirstLine();
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetTextSnapshot() => Document.Text;

    /// <summary>
    /// 替换当前文档的语法诊断；与项目诊断合并后写入 Diagnostics。
    /// </summary>
    public void SetSyntaxDiagnostics(IReadOnlyList<WeftDiagnostic> diagnostics)
    {
        if (_syntaxDiagnostics.SequenceEqual(diagnostics))
        {
            return;
        }

        _syntaxDiagnostics = diagnostics;
        MergeDiagnostics();
    }

    /// <summary>
    /// 替换当前文档的项目诊断；与语法诊断合并后写入 Diagnostics。
    /// </summary>
    public void SetProjectDiagnostics(IReadOnlyList<WeftDiagnostic> diagnostics)
    {
        if (_projectDiagnostics.SequenceEqual(diagnostics))
        {
            return;
        }

        _projectDiagnostics = diagnostics;
        MergeDiagnostics();
    }

    public int GetOffset(int line, int column)
    {
        if (Document.LineCount == 0)
        {
            return 0;
        }

        var lineNumber = Math.Clamp(line, 1, Document.LineCount);
        var documentLine = Document.GetLineByNumber(lineNumber);
        var clampedColumn = Math.Clamp(column, 1, documentLine.Length + 1);
        return documentLine.Offset + clampedColumn - 1;
    }

    partial void OnFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(FileName));
        UpdateIcon();
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(FileName));

    private void ReplaceDocument(string text)
    {
        Document.Changed -= OnDocumentChanged;
        Document = new TextDocument(text);
        Document.Changed += OnDocumentChanged;
        Version++;
        RefreshFirstLine();
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        Version++;

        if (_suppressDocumentChange)
        {
            return;
        }

        IsModified = true;

        if (e.Offset <= _firstLineEnd)
        {
            var firstLine = GetFirstLine();
            _firstLineEnd = GetFirstLineEnd();

            if (!string.Equals(_lastFirstLine, firstLine, StringComparison.Ordinal))
            {
                _lastFirstLine = firstLine;
                Title = ParseTitle(firstLine, GetFallbackTitle());
            }
        }

        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MergeDiagnostics()
    {
        var merged = _syntaxDiagnostics
            .Concat(_projectDiagnostics)
            .DistinctBy(static diagnostic => (
                diagnostic.Code,
                diagnostic.Span.Start,
                diagnostic.Span.Length,
                diagnostic.Severity,
                diagnostic.Message))
            .ToList();

        if (!Diagnostics.SequenceEqual(merged))
        {
            Diagnostics = merged;
        }
    }

    private string GetFallbackTitle()
        => string.IsNullOrEmpty(FilePath) ? Title : Path.GetFileName(FilePath);

    private void RefreshFirstLine()
    {
        _lastFirstLine = GetFirstLine();
        _firstLineEnd = GetFirstLineEnd();
        Title = ParseTitle(_lastFirstLine, GetFallbackTitle());
    }

    private string GetFirstLine()
    {
        if (Document.LineCount == 0)
        {
            return string.Empty;
        }

        return Document.GetText(Document.GetLineByNumber(1)).TrimEnd('\r');
    }

    private int GetFirstLineEnd()
        => Document.LineCount == 0 ? 0 : Document.GetLineByNumber(1).EndOffset;

    private void UpdateIcon()
    {
        var icon = _iconProvider.Resolve(
            string.IsNullOrEmpty(FilePath) ? "welcome.weft" : FilePath,
            false,
            false);
        IconGeometry = icon.Geometry;
        IconBrush = icon.Brush;
    }

    private static string ParseTitle(string firstLine, string fallback)
    {
        var match = TitleRegex.Match(firstLine);
        return match.Success ? match.Groups["title"].Value.Trim() : fallback;
    }
}
