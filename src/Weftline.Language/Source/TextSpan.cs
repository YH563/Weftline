namespace Weftline.Language.Source;

/// <summary>
/// A contiguous range in the source text, measured in character offsets.
/// Used for diagnostics, syntax node spans, and text edits.
/// </summary>
public readonly record struct TextSpan(int Start, int Length)
{
    /// <summary>Exclusive end offset of the span.</summary>
    public int End => Start + Length;

    /// <summary>Creates a span from start and end offsets.</summary>
    public static TextSpan FromBounds(int start, int end) => new(start, end - start);

    /// <summary>Creates an empty span at the given position.</summary>
    public static TextSpan Empty(int position) => new(position, 0);

    public override string ToString() => $"[{Start}..{End})";
}
