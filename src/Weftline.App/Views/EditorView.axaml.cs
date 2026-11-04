using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Weftline.App.Controls;
using Weftline.App.Services;
using Weftline.App.ViewModels;

namespace Weftline.App.Views;

public partial class EditorView : UserControl
{
    private readonly WeftHighlightingTransformer _highlighter = new();
    private EditorHostViewModel? _host;
    private bool _updating;

    public EditorView()
    {
        InitializeComponent();

        TextEditor.TextArea.TextView.LineTransformers.Add(_highlighter);
        ReplaceLeftMargins();
        TextEditor.TextChanged += OnEditorTextChanged;
        TextEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        TextEditor.TextArea.SelectionChanged += OnSelectionChanged;
        DataContextChanged += OnDataContextChanged;
        ThemeService.Changed += OnThemeChanged;
        ApplyTheme(ThemeService.Current);
    }

    public void OpenFile(string path)
    {
        if (DataContext is not EditorHostViewModel host)
        {
            host = new EditorHostViewModel();
            DataContext = host;
        }

        host.OpenFile(path);
        LoadSelectedDocument();
        TextEditor.Focus();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is null)
        {
            DataContext = new EditorHostViewModel();
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_host is not null)
        {
            _host.PropertyChanged -= OnHostPropertyChanged;
        }

        _host = DataContext as EditorHostViewModel;

        if (_host is not null)
        {
            _host.PropertyChanged += OnHostPropertyChanged;
            LoadSelectedDocument();
        }
    }

    private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorHostViewModel.SelectedDocument))
        {
            LoadSelectedDocument();
        }
    }

    private void LoadSelectedDocument()
    {
        if (_host?.SelectedDocument is not { } document)
        {
            _updating = true;
            TextEditor.Document = new TextDocument();
            _updating = false;
            return;
        }

        _updating = true;
        TextEditor.Document = new TextDocument(document.Text);
        _updating = false;

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

    private void SetCaret(EditorViewModel document)
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
                margins.RemoveAt(index);
                margins.Insert(index, new WeftLineNumberMargin
                {
                    MinWidthInDigits = 5,
                });
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

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyTheme(ThemeService.Current);

    private void ApplyTheme(ThemePalette palette)
    {
        TextEditor.Background = palette.EditorBackground;
        TextEditor.Foreground = palette.EditorForeground;
        TextEditor.LineNumbersForeground = palette.EditorLineNumber;
        TextEditor.TextArea.TextView.CurrentLineBackground = palette.IsDark
            ? new SolidColorBrush(Color.Parse("#1D2430"))
            : new SolidColorBrush(Color.Parse("#F4F8FF"));
        TextEditor.TextArea.TextView.CurrentLineBorder = new Pen(
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#2D3A4F" : "#E3ECFA")),
            1);

        _highlighter.ApplyTheme(palette);
        TextEditor.TextArea.TextView.Redraw();
    }
}
