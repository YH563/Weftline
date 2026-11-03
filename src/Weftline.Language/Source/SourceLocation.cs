namespace Weftline.Language.Source;

/// <summary>
/// 一个带文件路径的源码位置。
/// A source location paired with its file path.
/// </summary>
public readonly record struct SourceLocation(
    string FilePath,
    TextSpan Span);
