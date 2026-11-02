namespace Weftline.Language.Source;

/// <summary>
/// 源码中的一段连续范围，以字符偏移量计量。
/// 用于诊断、语法节点范围和文本编辑。
/// A contiguous range in the source text, measured in character offsets.
/// Used for diagnostics, syntax node spans, and text edits.
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>
    /// 范围的结束偏移量（不包含）。
    /// Exclusive end offset of the span.
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    /// 根据起始和结束偏移量创建范围。
    /// Creates a span from start and end offsets.
    /// </summary>
    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    /// <summary>
    /// 在指定位置创建一个空范围。
    /// Creates an empty span at the given position.
    /// </summary>
    public static TextSpan Empty(int position) => new(position, 0);

    public override string ToString() => $"[{Start}..{End})";
}
