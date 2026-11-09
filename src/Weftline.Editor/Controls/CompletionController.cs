using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using Weftline.Editor.Services;
using Weftline.Models;

namespace Weftline.Editor.Controls;

/// <summary>
/// 基于语言服务补全；编辑器只负责触发、替换区间和 AvaloniaEdit 窗口。
/// </summary>
public sealed class CompletionController
{
    private readonly TextEditor _editor;
    private readonly Func<string?> _filePathProvider;
    private readonly Func<IWeftEditorLanguageService?> _languageServiceProvider;
    private CompletionWindow? _window;
    private CancellationTokenSource? _completionCts;

    public CompletionController(
        TextEditor editor,
        Func<string?> filePathProvider,
        Func<IWeftEditorLanguageService?> languageServiceProvider)
    {
        _editor = editor;
        _filePathProvider = filePathProvider;
        _languageServiceProvider = languageServiceProvider;

        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.AddHandler(
            InputElement.KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel);
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        var text = e.Text;

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_window is not null)
        {
            if (IsWordChar(text[0]))
            {
                return;
            }

            _completionCts?.Cancel();
            _window.Close();
            _window = null;
        }

        if (text == "@" || text == "{")
        {
            _ = ShowCompletionAsync();
            return;
        }

        if (text == " ")
        {
            var document = _editor.Document;
            var offset = _editor.CaretOffset;

            if (document is not null)
            {
                var lineStart = document.GetLineByOffset(offset).Offset;
                var linePrefix = document.GetText(lineStart, offset - lineStart);

                if (linePrefix.EndsWith("-> ", StringComparison.Ordinal) ||
                    linePrefix.TrimEnd().EndsWith("@scene", StringComparison.Ordinal) ||
                    IsVarValueTrigger(linePrefix))
                {
                    _ = ShowCompletionAsync();
                }
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            _ = ShowCompletionAsync();
        }
    }

    private async Task ShowCompletionAsync()
    {
        var languageService = _languageServiceProvider();
        var filePath = _filePathProvider();
        var document = _editor.Document;

        if (languageService is null || string.IsNullOrEmpty(filePath) || document is null)
        {
            return;
        }

        _completionCts?.Cancel();

        var cts = new CancellationTokenSource();
        _completionCts = cts;
        var token = cts.Token;
        var offset = _editor.CaretOffset;
        var wordStart = FindWordStart(document.Text, offset);
        var context = new WeftCompletionContext(filePath, document.Text, offset, wordStart);

        WeftCompletionResult result;
        bool cancelled;

        try
        {
            result = await languageService.GetCompletionsAsync(context, token);
            cancelled = token.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            return;
        }
        finally
        {
            if (ReferenceEquals(_completionCts, cts))
            {
                _completionCts = null;
            }

            cts.Dispose();
        }

        if (cancelled ||
            result.Items.Count == 0 ||
            _editor.CaretOffset != offset)
        {
            return;
        }

        _window?.Close();
        _window = null;

        var window = new CompletionWindow(_editor.TextArea)
        {
            StartOffset = result.ReplacementStart,
            EndOffset = offset,
        };
        window.CompletionList.IsFiltering = true;

        foreach (var item in result.Items)
        {
            window.CompletionList.CompletionData.Add(new WeftCompletionData(item));
        }

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        };
        window.Opened += (_, _) => ApplyCompletionTheme(window);
        window.CompletionList.TemplateApplied += (_, _) => ApplyCompletionTheme(window);
        _window = window;
        window.Show();
    }

    private static void ApplyCompletionTheme(CompletionWindow window)
    {
        var list = window.CompletionList;
        var background = new SolidColorBrush(Color.Parse("#FFFFFF"));
        var foreground = new SolidColorBrush(Color.Parse("#333333"));
        var borderBrush = new SolidColorBrush(Color.Parse("#D8DCE3"));

        list.Background = background;
        list.Foreground = foreground;
        list.BorderBrush = borderBrush;
        list.BorderThickness = new Thickness(1);
        list.CornerRadius = new CornerRadius(0);
        list.FocusAdorner = null;
        window.FocusAdorner = null;

        if (window.Child is Border popupBorder)
        {
            popupBorder.Background = background;
            popupBorder.BorderBrush = borderBrush;
            popupBorder.BorderThickness = new Thickness(1);
            popupBorder.CornerRadius = new CornerRadius(0);
            popupBorder.BoxShadow = default;
        }

        var rootBorder = list.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => ReferenceEquals(border.TemplatedParent, list))
            ?? list.GetVisualDescendants().OfType<Border>().FirstOrDefault();

        if (rootBorder is not null)
        {
            rootBorder.Background = background;
            rootBorder.BorderBrush = borderBrush;
            rootBorder.BorderThickness = new Thickness(1);
            rootBorder.CornerRadius = new CornerRadius(0);
            rootBorder.BoxShadow = default;
        }

        if (list.ListBox is { } listBox)
        {
            listBox.Background = background;
            listBox.Foreground = foreground;
            listBox.BorderBrush = Brushes.Transparent;
            listBox.BorderThickness = new Thickness(0);
            listBox.CornerRadius = new CornerRadius(0);
            listBox.FocusAdorner = null;

            if (!listBox.Classes.Contains("weft-completion-items"))
            {
                listBox.Classes.Add("weft-completion-items");
                listBox.Padding = new Thickness(2);
                listBox.ItemTemplate = CreateItemDataTemplate(foreground);
                listBox.ItemContainerTheme = CreateItemContainerTheme(foreground);
            }
        }

        if (list.ScrollViewer is { } scrollViewer)
        {
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        }

        foreach (var scrollBar in list.GetVisualDescendants().OfType<ScrollBar>())
        {
            scrollBar.IsVisible = false;
        }
    }

    private static IDataTemplate CreateItemDataTemplate(IBrush foreground)
        => new FuncDataTemplate<object>((data, _) =>
            new TextBlock
            {
                Text = data is ICompletionData item
                    ? item.Content?.ToString() ?? item.Text
                    : data?.ToString() ?? string.Empty,
                Foreground = foreground,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            });

    private static ControlTheme CreateItemContainerTheme(IBrush foreground)
    {
        var hover = new SolidColorBrush(Color.Parse("#F2F4F7"));
        var selected = new SolidColorBrush(Color.Parse("#E8F0FE"));

        var theme = new ControlTheme(typeof(ListBoxItem))
        {
            Setters =
            {
                new Setter(TemplatedControl.ForegroundProperty, foreground),
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 2)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                new Setter(Control.FocusAdornerProperty, null),
                new Setter(Layoutable.MinHeightProperty, 22d),
                new Setter(
                    TemplatedControl.TemplateProperty,
                    CreateItemContainerTemplate()),
            },
        };

        theme.Children.Add(new Style(selector => selector.Nesting().Class(":pointerover"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, hover),
            },
        });

        theme.Children.Add(new Style(selector => selector.Nesting().Class(":selected"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, selected),
                new Setter(TemplatedControl.ForegroundProperty, foreground),
            },
        });

        return theme;
    }

    private static FuncControlTemplate<ListBoxItem> CreateItemContainerTemplate()
        => new((item, _) =>
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            border.Bind(
                Border.BackgroundProperty,
                new Binding(nameof(TemplatedControl.Background)) { Source = item });
            border.Bind(
                Border.PaddingProperty,
                new Binding(nameof(TemplatedControl.Padding)) { Source = item });

            var presenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
            };
            presenter.Bind(
                ContentPresenter.ContentProperty,
                new Binding(nameof(ContentControl.Content)) { Source = item });
            presenter.Bind(
                ContentPresenter.ContentTemplateProperty,
                new Binding(nameof(ContentControl.ContentTemplate)) { Source = item });

            border.Child = presenter;
            return border;
        });

    private static bool IsVarValueTrigger(string linePrefix)
    {
        if (!linePrefix.EndsWith(' '))
        {
            return false;
        }

        var trimmed = linePrefix.Trim();

        if (!trimmed.StartsWith("@var ", StringComparison.Ordinal) &&
            !trimmed.StartsWith("@var\t", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Contains('['))
        {
            return false;
        }

        var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length == 2;
    }

    private static int FindWordStart(string text, int offset)
    {
        var index = offset;

        while (index > 0 && IsWordChar(text[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static bool IsWordChar(char value)
        => char.IsLetterOrDigit(value) || value == '_' || value == '@';

    private sealed class WeftCompletionData : ICompletionData
    {
        private readonly WeftCompletionItem _item;

        public WeftCompletionData(WeftCompletionItem item)
        {
            _item = item;
        }

        public IImage? Image => null;

        public string Text => _item.Text;

        public object Content => _item.Label ?? _item.Text;

        public object Description => _item.Description ?? string.Empty;

        public double Priority => _item.Priority;

        public void Complete(
            TextArea textArea,
            ISegment completionSegment,
            EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, _item.Text);
        }
    }
}
