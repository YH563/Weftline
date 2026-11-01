namespace Weftline.Language.Source;

/// <summary>
/// 源文本：保存完整文本、按行切分的结果，以及偏移量与行列的换算。
///
/// 编译器不会修改它。整个前端都把它当作只读的“事实来源”。
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

    public string Text => _text;

    public string? FilePath { get; }

    public IReadOnlyList<SourceLine> Lines => _lines;

    public int Length => _text.Length;

    public static SourceText From(string text, string? filePath = null)
        => new(text ?? string.Empty, filePath);

    public string GetText(TextSpan span)
    {
        if (span.Start < 0 || span.End > _text.Length || span.Length < 0)
        {
            return string.Empty;
        }

        return _text.Substring(span.Start, span.Length);
    }

    public LinePosition GetLinePosition(int position)
    {
        if (_lines.Length == 0)
        {
            return new LinePosition(1, 1);
        }

        position = Math.Clamp(position, 0, _text.Length);

        // 找最后一个满足 Span.Start <= position 的行。
        // 这比按行尾二分更简单，也能正确处理换行边界与 EOF。
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
/// 一行源码。Span 不含换行，SpanIncludingLineBreak 含换行。
/// </summary>
public readonly record struct SourceLine(
    string Text,
    TextSpan Span,
    TextSpan SpanIncludingLineBreak,
    int Number)
{
    public override string ToString() => $"{Number}: {Text}";
}

/// <summary>从 1 开始的行列位置。</summary>
public readonly record struct LinePosition(int Line, int Character)
{
    public override string ToString() => $"{Line}:{Character}";
}
