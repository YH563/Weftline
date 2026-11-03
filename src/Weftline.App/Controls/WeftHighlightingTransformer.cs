using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Weftline.App.Services;

namespace Weftline.App.Controls;

public sealed class WeftHighlightingTransformer : DocumentColorizingTransformer
{
    private static readonly Regex DirectiveRegex = new(@"@[A-Za-z_]\w*", RegexOptions.Compiled);
    private static readonly Regex ReferenceRegex = new(@"\{[^}]+\}", RegexOptions.Compiled);
    private static readonly Regex StringRegex = new("\"[^\"]*\"", RegexOptions.Compiled);

    public IBrush DirectiveBrush { get; set; } = Brushes.Transparent;

    public IBrush CommentBrush { get; set; } = Brushes.Transparent;

    public IBrush ReferenceBrush { get; set; } = Brushes.Transparent;

    public IBrush StringBrush { get; set; } = Brushes.Transparent;

    public void ApplyTheme(ThemePalette palette)
    {
        DirectiveBrush = palette.Directive;
        CommentBrush = palette.Comment;
        ReferenceBrush = palette.Reference;
        StringBrush = palette.String;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text.TrimStart().StartsWith("//", StringComparison.Ordinal))
        {
            ChangeLinePart(
                line.Offset,
                line.EndOffset,
                element => element.TextRunProperties.SetForegroundBrush(CommentBrush));
            return;
        }

        ColorizeMatches(text, line.Offset, DirectiveRegex, DirectiveBrush);
        ColorizeMatches(text, line.Offset, ReferenceRegex, ReferenceBrush);
        ColorizeMatches(text, line.Offset, StringRegex, StringBrush);
    }

    private void ColorizeMatches(string text, int offset, Regex regex, IBrush brush)
    {
        foreach (Match match in regex.Matches(text))
        {
            ChangeLinePart(
                offset + match.Index,
                offset + match.Index + match.Length,
                element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }
}
