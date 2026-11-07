using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using Weftline.Editor.Services;
using Weftline.Models;

namespace Weftline.Editor.Controls;

/// <summary>
/// 悬停提示：优先显示诊断，其次查询语言服务。
/// </summary>
public sealed class HoverController : IDisposable
{
    private static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(350);

    private readonly TextEditor _editor;
    private readonly Func<string?> _filePathProvider;
    private readonly Func<IReadOnlyList<WeftDiagnostic>> _diagnosticsProvider;
    private readonly Func<IWeftEditorLanguageService?> _languageServiceProvider;
    private readonly DispatcherTimer _timer;

    private int _pendingOffset = -1;
    private int _shownOffset = -1;

    public HoverController(
        TextEditor editor,
        Func<string?> filePathProvider,
        Func<IReadOnlyList<WeftDiagnostic>> diagnosticsProvider,
        Func<IWeftEditorLanguageService?> languageServiceProvider)
    {
        _editor = editor;
        _filePathProvider = filePathProvider;
        _diagnosticsProvider = diagnosticsProvider;
        _languageServiceProvider = languageServiceProvider;

        _timer = new DispatcherTimer { Interval = HoverDelay };
        _timer.Tick += OnTimerTick;

        _editor.TextArea.TextView.PointerMoved += OnPointerMoved;
        _editor.TextArea.TextView.PointerExited += OnPointerExited;
    }

    public void Hide()
    {
        _timer.Stop();
        _pendingOffset = -1;
        CloseToolTip();
    }

    public void Dispose()
    {
        Hide();
        _timer.Tick -= OnTimerTick;
        _editor.TextArea.TextView.PointerMoved -= OnPointerMoved;
        _editor.TextArea.TextView.PointerExited -= OnPointerExited;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var offset = ResolveOffset(e);

        if (offset < 0)
        {
            Hide();
            return;
        }

        if (offset == _pendingOffset)
        {
            return;
        }

        _pendingOffset = offset;
        _timer.Stop();
        CloseToolTip();
        _timer.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e) => Hide();

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();

        var offset = _pendingOffset;

        if (offset < 0 || offset == _shownOffset)
        {
            return;
        }

        var diagnostic = FindDiagnostic(offset);
        if (diagnostic is not null)
        {
            Show($"{diagnostic.Code}: {diagnostic.Message}");
            return;
        }

        var languageService = _languageServiceProvider();
        var filePath = _filePathProvider();

        if (languageService is null ||
            string.IsNullOrEmpty(filePath) ||
            _editor.Document is not { } document)
        {
            Hide();
            return;
        }

        var context = new WeftHoverContext(filePath, document.Text, offset);
        _ = ShowHoverAsync(languageService, context, offset);
    }

    private async Task ShowHoverAsync(
        IWeftEditorLanguageService languageService,
        WeftHoverContext context,
        int offset)
    {
        WeftHoverInfo? info;

        try
        {
            info = await languageService.GetHoverAsync(context, CancellationToken.None);
        }
        catch
        {
            return;
        }

        if (info is null || _pendingOffset != offset)
        {
            return;
        }

        Show(info.Title, info.Detail, info.Location);
    }

    private void Show(string title, string? detail = null, string? location = null)
    {
        var text = title;

        if (!string.IsNullOrWhiteSpace(detail))
        {
            text += "\n" + detail;
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            text += "\n" + location;
        }

        ToolTip.SetTip(_editor, text);
        ToolTip.SetIsOpen(_editor, true);
        _shownOffset = _pendingOffset;
    }

    private WeftDiagnostic? FindDiagnostic(int offset)
    {
        foreach (var diagnostic in _diagnosticsProvider())
        {
            var end = diagnostic.Span.Start + Math.Max(diagnostic.Span.Length, 1);
            if (offset >= diagnostic.Span.Start && offset < end)
            {
                return diagnostic;
            }
        }

        return null;
    }

    private int ResolveOffset(PointerEventArgs e)
    {
        var textView = _editor.TextArea.TextView;

        if (textView.Document is not { } document)
        {
            return -1;
        }

        var position = textView.GetPosition(e.GetPosition(textView) + textView.ScrollOffset);

        return position is null
            ? -1
            : Math.Clamp(document.GetOffset(position.Value.Location), 0, document.TextLength);
    }

    private void CloseToolTip()
    {
        if (_shownOffset < 0)
        {
            return;
        }

        _shownOffset = -1;
        ToolTip.SetIsOpen(_editor, false);
    }
}
