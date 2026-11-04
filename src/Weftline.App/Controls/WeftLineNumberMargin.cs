using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace Weftline.App.Controls;

public sealed class WeftLineNumberMargin : LineNumberMargin
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#F5F6F8"));

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width + 16, size.Height);
    }

    public override void Render(DrawingContext drawingContext)
    {
        var textView = TextView;
        var renderSize = Bounds.Size;
        drawingContext.FillRectangle(BackgroundBrush, new Rect(renderSize));

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
            drawingContext.DrawText(text, new Point(leftPadding, y + offsetY));
        }
    }
}
