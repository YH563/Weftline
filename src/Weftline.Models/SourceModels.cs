namespace Weftline.Models;

/// <summary>
/// 源码中的一段连续范围（UTF-16 code unit）。
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>
    /// 开区间终点：Start + Length。
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    /// 由起止位置创建范围。
    /// </summary>
    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    /// <summary>
    /// 在指定位置创建空范围。
    /// </summary>
    public static TextSpan Empty(int position) => new(position, 0);

    public override string ToString() => $"[{Start}..{End})";
}

/// <summary>
/// 带文件路径的源码位置。
/// </summary>
public readonly record struct SourceLocation(string FilePath, TextSpan Span);
