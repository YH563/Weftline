namespace Weftline.Language.Source;

/// <summary>
/// 源码文本。保存完整文本、行拆分，以及偏移量与行/列位置之间的转换。
/// 在编译期间不可变。
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

    /// <summary>
    /// 完整源码文本。
    /// The full source text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    /// 源码文件路径；内存中的文本为 null。
    /// Source file path, or null for in-memory text.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// 拆分后的行；行号从 1 开始。
    /// Lines after splitting; line numbers start at 1.
    /// </summary>
    public IReadOnlyList<SourceLine> Lines => _lines;

    /// <summary>
    /// 源码文本的总字符数。
    /// Total character count of the source text.
    /// </summary>
    public int Length => _text.Length;

    /// <summary>
    /// 从字符串创建源码文本。
    /// Creates source text from a string.
    /// </summary>
    public static SourceText From(string text, string? filePath = null)
        => new(text ?? string.Empty, filePath);

    /// <summary>
    /// 返回指定范围对应的源码片段。
    /// Returns the source slice for the given span.
    /// </summary>
    public string GetText(TextSpan span)
    {
        if (span.Start < 0 || span.End > _text.Length || span.Length < 0)
        {
            return string.Empty;
        }

        return _text.Substring(span.Start, span.Length);
    }

    /// <summary>
    /// 将字符偏移量转换为从 1 开始的行/列位置。
    /// Converts a character offset to a 1-based line/column position.
    /// </summary>
    public LinePosition GetLinePosition(int position)
    {
        if (_lines.Length == 0)
        {
            return new LinePosition(1, 1);
        }

        position = Math.Clamp(position, 0, _text.Length);

        // 查找 Span.Start <= position 的最后一行。
        // 这比按行尾查找更简单，也能处理换行边界和 EOF。
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
    /// 支持 \n、\r\n 和 \r 换行。始终保留最后一行，使 EOF 有稳定的行号。
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
/// 一行源码。Span 覆盖不含换行符的文本；
/// SpanIncludingLineBreak 还覆盖换行符。
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

/// <summary>
/// 从 1 开始的行/列位置。
/// A 1-based line/column position.
/// </summary>
public readonly record struct LinePosition(int Line, int Character)
{
    public override string ToString() => $"{Line}:{Character}";
}
