using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
                    linePrefix.TrimEnd().EndsWith("@scene", StringComparison.Ordinal))
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

        var offset = _editor.CaretOffset;
        var wordStart = FindWordStart(document.Text, offset);
        var context = new WeftCompletionContext(filePath, document.Text, offset, wordStart);

        WeftCompletionResult result;

        try
        {
            result = await languageService.GetCompletionsAsync(context, CancellationToken.None);
        }
        catch
        {
            return;
        }

        if (result.Items.Count == 0 || _editor.CaretOffset != offset)
        {
            return;
        }

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

        window.Closed += (_, _) => _window = null;
        _window = window;
        window.Show();
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
