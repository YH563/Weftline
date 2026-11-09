using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Weftline.Editor.Rendering;
using Weftline.Models;

namespace Weftline.Editor.Controls;

/// <summary>
/// 在当前文档上绘制诊断波浪线。
/// </summary>
public sealed class DiagnosticRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private IReadOnlyList<WeftDiagnostic> _diagnostics = [];
    private IPen _errorPen = new Pen(Brushes.Red, 1);
    private IPen _warningPen = new Pen(Brushes.Orange, 1);
    private IPen _infoPen = new Pen(Brushes.DodgerBlue, 1);

    public DiagnosticRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void ApplyTheme(EditorTheme theme)
    {
        _errorPen = new Pen(theme.DiagnosticError, 1);
        _warningPen = new Pen(theme.DiagnosticWarning, 1);
        _infoPen = new Pen(theme.DiagnosticInfo, 1);
    }

    public void Update(IReadOnlyList<WeftDiagnostic> diagnostics)
    {
        if (_diagnostics.SequenceEqual(diagnostics))
        {
            return;
        }

        _diagnostics = diagnostics;
        _editor.TextArea.TextView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_diagnostics.Count == 0 ||
            textView.Document is not { } document ||
            document.TextLength == 0)
        {
            return;
        }

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0)
        {
            return;
        }

        var viewStart = visualLines[0].FirstDocumentLine.Offset;
        var viewEnd = visualLines[visualLines.Count - 1].LastDocumentLine.EndOffset;

        foreach (var diagnostic in _diagnostics)
        {
            var diagnosticEnd = diagnostic.Span.Start + diagnostic.Span.Length;

            if (diagnosticEnd < viewStart || diagnostic.Span.Start > viewEnd)
            {
                continue;
            }

            var start = Math.Clamp(diagnostic.Span.Start, 0, document.TextLength);
            var end = Math.Clamp(diagnosticEnd, start, document.TextLength);

            if (start == end)
            {
                end = Math.Min(start + 1, document.TextLength);
            }

            var segment = new TextSegment
            {
                StartOffset = start,
                EndOffset = end,
            };

            var pen = ResolvePen(diagnostic.Severity);

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                DrawSquiggle(drawingContext, pen, rect);
            }
        }
    }

    private IPen ResolvePen(WeftDiagnosticSeverity severity)
        => severity switch
        {
            WeftDiagnosticSeverity.Error => _errorPen,
            WeftDiagnosticSeverity.Warning => _warningPen,
            _ => _infoPen,
        };

    private static void DrawSquiggle(DrawingContext drawingContext, IPen pen, Rect rect)
    {
        if (rect.Width <= 0)
        {
            return;
        }

        var baseline = rect.Bottom - 1.5;
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(rect.Left, baseline), false);

            var up = true;
            for (var x = rect.Left + 2; x < rect.Right; x += 2)
            {
                context.LineTo(new Point(x, baseline + (up ? -1.5 : 1.5)));
                up = !up;
            }

            context.LineTo(new Point(rect.Right, baseline));
        }

        drawingContext.DrawGeometry(null, pen, geometry);
    }
}
