using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit.Document;
using Weftline.App.Controls;
using Weftline.App.Services;
using Weftline.App.ViewModels;

namespace Weftline.App.Views;

public partial class EditorView : UserControl
{
    private readonly WeftHighlightingTransformer _highlighter = new();
    private DocumentViewModel? _document;
    private bool _updating;

    public EditorView()
    {
        InitializeComponent();
        Editor.TextArea.TextView.LineTransformers.Add(_highlighter);
        DataContextChanged += OnDataContextChanged;
        Editor.TextChanged += OnEditorTextChanged;
        ApplyTheme(ThemeService.Current);
        ThemeService.Changed += OnThemeChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_document is not null)
        {
            _document.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _document = DataContext as DocumentViewModel;

        if (_document is not null)
        {
            _updating = true;
            Editor.Document = new TextDocument(_document.Text);
            _updating = false;
            _document.PropertyChanged += OnDocumentPropertyChanged;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_updating || _document is null)
        {
            return;
        }

        _updating = true;
        _document.Text = Editor.Text;
        _updating = false;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DocumentViewModel.Text) ||
            _document is null ||
            _updating ||
            Editor.Text == _document.Text)
        {
            return;
        }

        _updating = true;
        Editor.Document = new TextDocument(_document.Text);
        _updating = false;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
        => ApplyTheme(ThemeService.Current);

    private void ApplyTheme(ThemePalette palette)
    {
        Editor.Background = palette.EditorBackground;
        Editor.Foreground = palette.EditorForeground;
        Editor.LineNumbersForeground = palette.EditorLineNumber;

        _highlighter.ApplyTheme(palette);
        Editor.TextArea.TextView.Redraw();
    }
}
