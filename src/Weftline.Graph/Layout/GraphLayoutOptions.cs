using Avalonia;

namespace Weftline.Graph.Layout;

/// <summary>
/// 用户对布局的手工覆盖。当前只覆盖模块矩形；章节容器由模块包围盒推导。
/// Manual layout overrides. Currently only module rectangles are overridden;
/// chapter containers are derived from the module bounds.
/// </summary>
public sealed record GraphLayoutOptions(
    IReadOnlyDictionary<string, Rect>? ModuleBounds = null,
    IReadOnlyDictionary<string, Rect>? ChapterBounds = null,
    Rect? EndBounds = null);
