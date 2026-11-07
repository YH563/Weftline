using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Weftline.Editor.Rendering;

namespace Weftline.Editor.Controls;

public sealed class WeftLineNumberMargin : LineNumberMargin
{
    private static readonly IBrush DefaultBackgroundBrush =
        new SolidColorBrush(Color.Parse("#F5F6F8"));

    private static readonly IReadOnlySet<int> EmptyLines = new HashSet<int>();

    private IBrush _backgroundBrush = DefaultBackgroundBrush;
    private IBrush _errorBrush = Brushes.Transparent;
    private IBrush _warningBrush = Brushes.Transparent;
    private IBrush _infoBrush = Brushes.Transparent;
    private IReadOnlySet<int> _errorLines = EmptyLines;
    private IReadOnlySet<int> _warningLines = EmptyLines;
    private IReadOnlySet<int> _infoLines = EmptyLines;

    public IBrush BackgroundBrush
    {
        get => _backgroundBrush;
        set
        {
            _backgroundBrush = value;
            InvalidateVisual();
        }
    }

    public void ApplyTheme(EditorTheme theme)
    {
        _backgroundBrush = theme.LineNumberBackground;
        _errorBrush = theme.DiagnosticError;
        _warningBrush = theme.DiagnosticWarning;
        _infoBrush = theme.DiagnosticInfo;
        InvalidateVisual();
    }

    public void SetMarkers(
        IReadOnlySet<int> errorLines,
        IReadOnlySet<int> warningLines,
        IReadOnlySet<int> infoLines)
    {
        _errorLines = errorLines;
        _warningLines = warningLines;
        _infoLines = infoLines;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width + 16, size.Height);
    }

    public override void Render(DrawingContext drawingContext)
    {
        var textView = TextView;
        var renderSize = Bounds.Size;
        drawingContext.FillRectangle(_backgroundBrush, new Rect(renderSize));

        if (textView is not { VisualLinesValid: true })
        {
            return;
        }

        var foreground = GetValue(TextBlock.ForegroundProperty);
        const double leftPadding = 4;
        var offsetY = -textView.VerticalOffset;

        foreach (var line in textView.VisualLines)
        {
            var lineNumber = line.FirstDocumentLine.LineNumber;
            var text = new FormattedText(
                lineNumber.ToString(CultureInfo.CurrentCulture),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface,
                EmSize,
                foreground);
            var y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop);

            var markerBrush = ResolveMarker(lineNumber);
            if (markerBrush is not null)
            {
                drawingContext.FillRectangle(
                    markerBrush,
                    new Rect(0, y, 3, Math.Max(1, text.Height)));
            }

            drawingContext.DrawText(text, new Point(leftPadding, y + offsetY));
        }
    }

    private IBrush? ResolveMarker(int lineNumber)
    {
        if (_errorLines.Contains(lineNumber))
        {
            return _errorBrush;
        }

        if (_warningLines.Contains(lineNumber))
        {
            return _warningBrush;
        }

        return _infoLines.Contains(lineNumber) ? _infoBrush : null;
    }
}
