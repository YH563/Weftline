using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Weftline.Editor.Controls;
using Weftline.Editor.Rendering;
using Weftline.Editor.Services;
using Weftline.Editor.ViewModels;
using Weftline.Models;

namespace Weftline.Editor.Views;

public partial class WeftEditorView : UserControl
{
    private readonly WeftHighlightingTransformer _highlighter = new();
    private readonly DiagnosticRenderer _diagnosticRenderer;
    private readonly HoverController _hoverController;
    private readonly CompletionController _completionController;
    private EditorHostViewModel? _host;
    private IEditorThemeSource? _themeSource;
    private WeftLineNumberMargin? _lineNumberMargin;
    private EditorDocumentViewModel? _document;
    private bool _updating;

    public WeftEditorView()
    {
        InitializeComponent();

        _diagnosticRenderer = new DiagnosticRenderer(TextEditor);

        TextEditor.TextArea.TextView.LineTransformers.Add(_highlighter);
        TextEditor.TextArea.TextView.BackgroundRenderers.Add(_diagnosticRenderer);
        ReplaceLeftMargins();
        TextEditor.TextChanged += OnEditorTextChanged;
        TextEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        TextEditor.TextArea.SelectionChanged += OnSelectionChanged;
        _hoverController = new HoverController(
            TextEditor,
            () => _document?.FilePath,
            () => _document?.Diagnostics ?? [],
            () => _host?.LanguageService);
        _completionController = new CompletionController(
            TextEditor,
            () => _document?.FilePath,
            () => _host?.LanguageService);
        DataContextChanged += OnDataContextChanged;
        ApplyTheme(EditorTheme.Default);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadSelectedDocument();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_host is not null)
        {
            _host.PropertyChanged -= OnHostPropertyChanged;
            _host.NavigationRequested -= OnNavigationRequested;
        }

        _host = DataContext as EditorHostViewModel;

        if (_host is not null)
        {
            _host.PropertyChanged += OnHostPropertyChanged;
            _host.NavigationRequested += OnNavigationRequested;
        }

        AttachThemeSource(_host?.ThemeSource);
        LoadSelectedDocument();
    }

    private void AttachThemeSource(IEditorThemeSource? themeSource)
    {
        if (_themeSource is not null)
        {
            _themeSource.Changed -= OnThemeChanged;
        }

        _themeSource = themeSource;

        if (_themeSource is not null)
        {
            _themeSource.Changed += OnThemeChanged;
        }

        ApplyTheme(_themeSource?.Current ?? EditorTheme.Default);
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorHostViewModel.SelectedDocument))
        {
            LoadSelectedDocument();
        }
    }

    private void OnNavigationRequested(object? sender, EditorDocumentViewModel document)
    {
        if (ReferenceEquals(_host?.SelectedDocument, document))
        {
            LoadSelectedDocument();
        }
    }

    private void LoadSelectedDocument()
    {
        AttachDocument(_host?.SelectedDocument);

        if (_host?.SelectedDocument is not { } document)
        {
            _updating = true;
            TextEditor.Document = new TextDocument();
            _updating = false;
            ApplyDiagnostics();
            return;
        }

        _updating = true;
        TextEditor.Document = new TextDocument(document.Text);
        _updating = false;
        ApplyDiagnostics();

        Dispatcher.UIThread.Post(
            () =>
            {
                if (!ReferenceEquals(_host?.SelectedDocument, document))
                {
                    return;
                }

                SetCaret(document);
            },
            DispatcherPriority.Loaded);
    }

    private void AttachDocument(EditorDocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _document = document;
        _hoverController.Hide();

        if (_document is not null)
        {
            _document.PropertyChanged += OnDocumentPropertyChanged;
        }

        ApplyDiagnostics();
    }

    private void SetCaret(EditorDocumentViewModel document)
    {
        if (TextEditor.Document is not { } textDocument || textDocument.LineCount == 0)
        {
            return;
        }

        var lineNumber = Math.Clamp(document.CaretLine, 1, textDocument.LineCount);
        var line = textDocument.GetLineByNumber(lineNumber);
        var column = Math.Clamp(document.CaretColumn, 1, line.Length + 1);
        TextEditor.CaretOffset = line.Offset + column - 1;
        TextEditor.SelectionLength = 0;
        document.SelectionLength = 0;
        TextEditor.ScrollToLine(lineNumber);
        TextEditor.TextArea.Caret.BringCaretToView();
    }

    private void ReplaceLeftMargins()
    {
        var margins = TextEditor.TextArea.LeftMargins;

        for (var index = margins.Count - 1; index >= 0; index--)
        {
            if (margins[index] is LineNumberMargin)
            {
                _lineNumberMargin = new WeftLineNumberMargin
                {
                    MinWidthInDigits = 5,
                };
                margins.RemoveAt(index);
                margins.Insert(index, _lineNumberMargin);
                continue;
            }

            if (!DottedLineMargin.IsDottedLineMargin(margins[index]))
            {
                continue;
            }

            margins.RemoveAt(index);
            margins.Insert(index, new Border
            {
                Width = 1,
                Margin = new Thickness(0, 0, 12, 0),
                Background = new SolidColorBrush(Color.Parse("#D8DCE3")),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
            });
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_updating || _host?.SelectedDocument is not { } document)
        {
            return;
        }

        document.UpdateText(TextEditor.Text, true);
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_host?.SelectedDocument is not { } document)
        {
            return;
        }

        document.CaretLine = TextEditor.TextArea.Caret.Line;
        document.CaretColumn = TextEditor.TextArea.Caret.Column;
        document.SelectionLength = TextEditor.SelectionLength;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_host?.SelectedDocument is not { } document)
        {
            return;
        }

        document.SelectionLength = TextEditor.SelectionLength;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorDocumentViewModel.Diagnostics))
        {
            ApplyDiagnostics();
        }
    }

    private void ApplyDiagnostics()
    {
        var diagnostics = _document?.Diagnostics ?? [];
        _diagnosticRenderer.Update(diagnostics);

        HashSet<int> errors = [];
        HashSet<int> warnings = [];
        HashSet<int> infos = [];

        if (TextEditor.Document is { } document)
        {
            foreach (var diagnostic in diagnostics)
            {
                var offset = Math.Clamp(diagnostic.Span.Start, 0, document.TextLength);
                var line = document.GetLineByOffset(offset).LineNumber;

                switch (diagnostic.Severity)
                {
                    case WeftDiagnosticSeverity.Error:
                        errors.Add(line);
                        break;
                    case WeftDiagnosticSeverity.Warning:
                        warnings.Add(line);
                        break;
                    default:
                        infos.Add(line);
                        break;
                }
            }
        }

        _lineNumberMargin?.SetMarkers(errors, warnings, infos);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
        => ApplyTheme(_themeSource?.Current ?? EditorTheme.Default);

    private void ApplyTheme(EditorTheme theme)
    {
        TextEditor.Background = theme.Background;
        TextEditor.Foreground = theme.Foreground;
        TextEditor.LineNumbersForeground = theme.LineNumberForeground;
        TextEditor.TextArea.TextView.CurrentLineBackground = theme.CurrentLineBackground;
        TextEditor.TextArea.TextView.CurrentLineBorder = new Pen(theme.CurrentLineBorder, 1);

        _lineNumberMargin?.ApplyTheme(theme);
        _highlighter.ApplyTheme(theme);
        _diagnosticRenderer.ApplyTheme(theme);
        TextEditor.TextArea.TextView.Redraw();
    }
}
