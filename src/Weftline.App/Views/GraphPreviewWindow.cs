using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Weftline.Graph.Controls;
using Weftline.Language.Ir;
using Weftline.Language.Projects;

namespace Weftline.App.Views;

/// <summary>
/// 开发用剧情图预览窗口。启动参数：--graph-preview &lt;folder&gt;。
/// Development-only story graph preview. Launch with: --graph-preview &lt;folder&gt;.
/// </summary>
public sealed class GraphPreviewWindow : Window
{
    public GraphPreviewWindow(string folder)
    {
        Title = $"Weftline Graph Preview - {Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))}";
        Width = 1440;
        Height = 900;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.Parse("#F5F7FB"));

        var result = ProjectCompiler.Compile(folder);

        if (result.Graph is null || result.Analysis is null)
        {
            Content = BuildError(result);
            return;
        }

        var projection = GraphProjectionBuilder.Build(result.Model, result.Graph, result.Analysis);

        var graph = new StoryGraphView
        {
            Projection = projection,
        };

        var headerText = new TextBlock
        {
            Text = $"{result.Model.Project.Name}  ·  " +
                   $"{projection.Chapters.Count} chapters  ·  " +
                   $"{projection.Modules.Count} modules  ·  " +
                   $"{projection.ChapterEdges.Count} chapter edges",
            Margin = new Thickness(16, 12),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#3B4352")),
        };

        var resetLayout = new Button
        {
            Content = "重置布局",
            Margin = new Thickness(16, 8),
            Padding = new Thickness(12, 4),
        };

        resetLayout.Click += (_, _) => graph.ResetLayout();

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };

        Grid.SetColumn(headerText, 0);
        Grid.SetColumn(resetLayout, 1);
        header.Children.Add(headerText);
        header.Children.Add(resetLayout);

        var scroll = new ScrollViewer
        {
            Content = graph,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
        };

        Grid.SetRow(header, 0);
        Grid.SetRow(scroll, 1);
        grid.Children.Add(header);
        grid.Children.Add(scroll);

        Content = grid;
    }

    private static Control BuildError(ProjectCompilationResult result)
    {
        var diagnostics = result.Diagnostics
            .Take(20)
            .Select(item => $"{item.Diagnostic.Code}: {item.Diagnostic.Message}")
            .ToList();

        var text = diagnostics.Count == 0
            ? "No graph was generated."
            : string.Join(Environment.NewLine, diagnostics);

        return new TextBlock
        {
            Text = text,
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#C65B5B")),
        };
    }
}
