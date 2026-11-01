namespace Weftline.Language.Source;

/// <summary>
/// 源码中的一段连续区间，单位是字符偏移。
/// 编译器的诊断、AST、后续的文本编辑都以它定位。
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    public static TextSpan Empty(int position) => new(position, 0);

    public override string ToString() => $"[{Start}..{End})";
}
