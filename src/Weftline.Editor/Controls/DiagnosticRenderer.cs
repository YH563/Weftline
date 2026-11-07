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
    private IBrush _errorBrush = Brushes.Red;
    private IBrush _warningBrush = Brushes.Orange;
    private IBrush _infoBrush = Brushes.DodgerBlue;

    public DiagnosticRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void ApplyTheme(EditorTheme theme)
    {
        _errorBrush = theme.DiagnosticError;
        _warningBrush = theme.DiagnosticWarning;
        _infoBrush = theme.DiagnosticInfo;
    }

    public void Update(IReadOnlyList<WeftDiagnostic> diagnostics)
    {
        _diagnostics = diagnostics;
        _editor.TextArea.TextView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_diagnostics.Count == 0 || textView.Document is not { } document)
        {
            return;
        }

        foreach (var diagnostic in _diagnostics)
        {
            if (document.TextLength == 0)
            {
                return;
            }

            var start = Math.Clamp(diagnostic.Span.Start, 0, document.TextLength);
            var end = Math.Clamp(
                diagnostic.Span.Start + diagnostic.Span.Length,
                start,
                document.TextLength);

            if (start == end)
            {
                end = Math.Min(start + 1, document.TextLength);
            }

            var segment = new TextSegment
            {
                StartOffset = start,
                EndOffset = end,
            };

            var pen = new Pen(ResolveBrush(diagnostic.Severity), 1);

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                DrawSquiggle(drawingContext, pen, rect);
            }
        }
    }

    private IBrush ResolveBrush(WeftDiagnosticSeverity severity)
        => severity switch
        {
            WeftDiagnosticSeverity.Error => _errorBrush,
            WeftDiagnosticSeverity.Warning => _warningBrush,
            _ => _infoBrush,
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
