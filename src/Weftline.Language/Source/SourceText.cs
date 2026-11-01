namespace Weftline.Language.Source;

/// <summary>
/// Source text. Stores the full text, line splits, and offset/line-position conversions.
/// It is immutable during compilation.
/// </summary>
public sealed class SourceText
{
    private readonly string _text;
    private readonly SourceLine[] _lines;

    private SourceText(string text, string? filePath)
    {
        _text = text;
        FilePath = filePath;
        _lines = SplitLines(text);
    }

    /// <summary>The full source text.</summary>
    public string Text => _text;

    /// <summary>Source file path, or null for in-memory text.</summary>
    public string? FilePath { get; }

    /// <summary>Lines after splitting; line numbers start at 1.</summary>
    public IReadOnlyList<SourceLine> Lines => _lines;

    /// <summary>Total character count of the source text.</summary>
    public int Length => _text.Length;

    /// <summary>Creates source text from a string.</summary>
    public static SourceText From(string text, string? filePath = null)
        => new(text ?? string.Empty, filePath);

    /// <summary>Returns the source slice for the given span.</summary>
    public string GetText(TextSpan span)
    {
        if (span.Start < 0 || span.End > _text.Length || span.Length < 0)
        {
            return string.Empty;
        }

        return _text.Substring(span.Start, span.Length);
    }

    /// <summary>Converts a character offset to a 1-based line/column position.</summary>
    public LinePosition GetLinePosition(int position)
    {
        if (_lines.Length == 0)
        {
            return new LinePosition(1, 1);
        }

        position = Math.Clamp(position, 0, _text.Length);

        // Find the last line whose Span.Start is <= position.
        // This is simpler than searching by line end and handles newline boundaries and EOF.
        var low = 0;
        var high = _lines.Length - 1;
        var found = 0;

        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (_lines[middle].Span.Start <= position)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        var line = _lines[found];
        var character = position - line.Span.Start + 1;
        return new LinePosition(line.Number, Math.Max(1, character));
    }

    /// <summary>
    /// Supports \n, \r\n, and \r line breaks. Always keeps a final line so EOF has a stable line number.
    /// </summary>
    private static SourceLine[] SplitLines(string text)
    {
        var lines = new List<SourceLine>();
        var lineStart = 0;
        var lineNumber = 1;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n'))
            {
                continue;
            }

            var lineEnd = i;
            var breakLength = 1;

            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                breakLength = 2;
                i++;
            }

            var lineText = text.Substring(lineStart, lineEnd - lineStart);
            var span = new TextSpan(lineStart, lineText.Length);
            var spanWithBreak = new TextSpan(lineStart, lineText.Length + breakLength);
            lines.Add(new SourceLine(lineText, span, spanWithBreak, lineNumber++));

            lineStart = lineEnd + breakLength;
        }

        var finalText = text.Substring(lineStart);
        var finalSpan = new TextSpan(lineStart, finalText.Length);
        lines.Add(new SourceLine(finalText, finalSpan, finalSpan, lineNumber));

        return lines.ToArray();
    }
}

/// <summary>
/// A source line. Span covers the text without the line break;
/// SpanIncludingLineBreak also covers the break.
/// </summary>
public readonly record struct SourceLine(
    string Text,
    TextSpan Span,
    TextSpan SpanIncludingLineBreak,
    int Number)
{
    public override string ToString() => $"{Number}: {Text}";
}

/// <summary>A 1-based line/column position.</summary>
public readonly record struct LinePosition(int Line, int Character)
{
    public override string ToString() => $"{Line}:{Character}";
}
