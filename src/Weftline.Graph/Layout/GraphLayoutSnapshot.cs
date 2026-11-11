using Avalonia;

namespace Weftline.Graph.Layout;

/// <summary>
/// 可持久化的手工布局快照：模块/章节矩形、END 矩形和缩放。
/// A persistable manual-layout snapshot: module/chapter rectangles, END, and zoom.
/// </summary>
public sealed record GraphLayoutSnapshot(
    IReadOnlyDictionary<string, Rect> ModuleBounds,
    IReadOnlyDictionary<string, Rect> ChapterBounds,
    Rect? EndBounds,
    double Zoom);
