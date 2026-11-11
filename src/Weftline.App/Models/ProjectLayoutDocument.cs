using Avalonia;
using Weftline.Graph.Layout;

namespace Weftline.App.Models;

/// <summary>
/// weftline.layout.json 的磁盘格式。
/// On-disk format for weftline.layout.json.
/// </summary>
public sealed class ProjectLayoutDocument
{
    public int Version { get; set; } = 1;

    public double Zoom { get; set; } = 1;

    public Dictionary<string, LayoutRect> Modules { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, LayoutRect> Chapters { get; set; } = new(StringComparer.Ordinal);

    public LayoutRect? End { get; set; }

    public static ProjectLayoutDocument FromSnapshot(GraphLayoutSnapshot snapshot)
        => new()
        {
            Zoom = snapshot.Zoom,
            Modules = snapshot.ModuleBounds.ToDictionary(
                static pair => pair.Key,
                static pair => LayoutRect.FromRect(pair.Value),
                StringComparer.Ordinal),
            Chapters = snapshot.ChapterBounds.ToDictionary(
                static pair => pair.Key,
                static pair => LayoutRect.FromRect(pair.Value),
                StringComparer.Ordinal),
            End = snapshot.EndBounds is { } end ? LayoutRect.FromRect(end) : null,
        };

    public GraphLayoutSnapshot ToSnapshot()
        => new(
            Modules.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToRect(),
                StringComparer.Ordinal),
            Chapters.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToRect(),
                StringComparer.Ordinal),
            End?.ToRect(),
            Zoom);
}

/// <summary>
/// 可 JSON 序列化的矩形。
/// A JSON-serializable rectangle.
/// </summary>
public sealed class LayoutRect
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public static LayoutRect FromRect(Rect rect)
        => new()
        {
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
        };

    public Rect ToRect() => new(X, Y, Width, Height);
}
