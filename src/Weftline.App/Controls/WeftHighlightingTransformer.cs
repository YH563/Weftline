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
    private static readonly Regex KeywordRegex = new(@"\b(?:true|false|END)\b", RegexOptions.Compiled);

    private IBrush _directiveBrush = Brushes.Transparent;
    private IBrush _commentBrush = Brushes.Transparent;
    private IBrush _referenceBrush = Brushes.Transparent;
    private IBrush _stringBrush = Brushes.Transparent;
    private IBrush _keywordBrush = Brushes.Transparent;

    public void ApplyTheme(ThemePalette palette)
    {
        _directiveBrush = palette.Directive;
        _commentBrush = palette.Comment;
        _referenceBrush = palette.Reference;
        _stringBrush = palette.String;
        _keywordBrush = palette.Directive;
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
                element => element.TextRunProperties.SetForegroundBrush(_commentBrush));
            return;
        }

        ColorizeMatches(text, line.Offset, DirectiveRegex, _directiveBrush);
        ColorizeMatches(text, line.Offset, ReferenceRegex, _referenceBrush);
        ColorizeMatches(text, line.Offset, StringRegex, _stringBrush);
        ColorizeMatches(text, line.Offset, KeywordRegex, _keywordBrush);
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
