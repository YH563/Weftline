using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Weftline.App.Models;
using Weftline.App.Services;

namespace Weftline.App.ViewModels;

public sealed class GraphViewModel
{
    public GraphViewModel(
        IReadOnlyList<SampleChapter> chapters,
        IReadOnlyList<SampleEdge> edges)
    {
        const double chapterX = 8;
        const double chapterWidth = 290;
        const double chapterHeader = 44;
        const double chapterPadding = 14;
        const double nodeHeight = 52;
        const double nodeGap = 10;

        var nodeList = new List<GraphNodeViewModel>();
        var chapterList = new List<GraphChapterViewModel>();
        var nodesById = new Dictionary<string, GraphNodeViewModel>(StringComparer.Ordinal);

        var y = 16.0;

        foreach (var chapter in chapters)
        {
            var chapterHeight = chapterHeader + chapter.Modules.Count * (nodeHeight + nodeGap) + chapterPadding;
            chapterList.Add(new GraphChapterViewModel(
                chapter.Title,
                chapterX,
                y,
                chapterWidth,
                chapterHeight));

            var nodeY = y + chapterHeader;

            foreach (var module in chapter.Modules)
            {
                var node = new GraphNodeViewModel(
                    module.Id,
                    module.Scene,
                    chapterX + 14,
                    nodeY,
                    chapterWidth - 28,
                    nodeHeight);

                nodeList.Add(node);
                nodesById[module.Id] = node;
                nodeY += nodeHeight + nodeGap;
            }

            y += chapterHeight + 26;
        }

        var edgeList = new List<GraphEdgeViewModel>();

        foreach (var edge in edges)
        {
            if (!nodesById.TryGetValue(edge.From, out var from) ||
                !nodesById.TryGetValue(edge.To, out var to))
            {
                continue;
            }

            var start = new Point(from.X + (from.Width / 2), from.Y + from.Height);
            var end = new Point(to.X + (to.Width / 2), to.Y);
            var labelX = ((start.X + end.X) / 2) + 8;
            var labelY = ((start.Y + end.Y) / 2) - 14;

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var bend = Math.Max(36, Math.Abs(end.Y - start.Y) * 0.18);
                context.BeginFigure(start, false);
                context.CubicBezierTo(
                    new Point(start.X + bend, start.Y + 24),
                    new Point(end.X + bend, end.Y - 24),
                    end);
                context.EndFigure(false);
            }

            edgeList.Add(new GraphEdgeViewModel(geometry, edge.Label, labelX, labelY, edge.External));
        }

        Chapters = new ObservableCollection<GraphChapterViewModel>(chapterList);
        Nodes = new ObservableCollection<GraphNodeViewModel>(nodeList);
        Edges = new ObservableCollection<GraphEdgeViewModel>(edgeList);
        CanvasWidth = chapterX + chapterWidth + 8;
        CanvasHeight = Math.Max(y + 16, 480);

        ApplyTheme(ThemeService.Current);
        ThemeService.Changed += OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
        => ApplyTheme(ThemeService.Current);

    private void ApplyTheme(ThemePalette palette)
    {
        foreach (var edge in Edges)
        {
            edge.Stroke = edge.External ? palette.GraphDanger : palette.GraphAccent;
        }
    }

    public double CanvasWidth { get; }

    public double CanvasHeight { get; }

    public ObservableCollection<GraphChapterViewModel> Chapters { get; }

    public ObservableCollection<GraphNodeViewModel> Nodes { get; }

    public ObservableCollection<GraphEdgeViewModel> Edges { get; }
}

public sealed record GraphChapterViewModel(
    string Title,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record GraphNodeViewModel(
    string Id,
    string Scene,
    double X,
    double Y,
    double Width,
    double Height);

public partial class GraphEdgeViewModel : ObservableObject
{
    [ObservableProperty]
    private IBrush _stroke = Brushes.Transparent;

    public GraphEdgeViewModel(
        Geometry geometry,
        string? label,
        double labelX,
        double labelY,
        bool external)
    {
        Geometry = geometry;
        Label = label;
        LabelX = labelX;
        LabelY = labelY;
        External = external;
    }

    public Geometry Geometry { get; }

    public string? Label { get; }

    public double LabelX { get; }

    public double LabelY { get; }

    public bool External { get; }

    public bool HasLabel => !string.IsNullOrEmpty(Label);
}
