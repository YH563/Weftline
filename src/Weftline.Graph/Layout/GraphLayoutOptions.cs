using Avalonia;

namespace Weftline.Graph.Layout;

/// <summary>
/// 用户对布局的手工覆盖：模块/章节矩形与 END 位置；未覆盖的章节仍由模块包围盒推导。
/// Manual layout overrides: module/chapter rectangles and the END position;
/// chapters without overrides are still derived from module bounds.
/// </summary>
public sealed record GraphLayoutOptions(
    IReadOnlyDictionary<string, Rect>? ModuleBounds = null,
    IReadOnlyDictionary<string, Rect>? ChapterBounds = null,
    Rect? EndBounds = null);
