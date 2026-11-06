using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Weftline.Graph.Controls;
using Weftline.Native;

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

        NativeProjectResponse response;

        try
        {
            response = CompileWithNativeCore(folder);
        }
        catch (Exception exception)
        {
            Content = BuildError($"Native core failed: {exception.Message}");
            return;
        }

        var projection = response.Projection?.ToGraphProjection();

        if (projection is null)
        {
            Content = BuildError(response);
            return;
        }

        var graph = new StoryGraphView
        {
            Projection = projection,
        };

        var projectName = string.IsNullOrWhiteSpace(response.ProjectName)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))
            : response.ProjectName;

        var headerText = new TextBlock
        {
            Text = $"{projectName}  ·  " +
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

    private static NativeProjectResponse CompileWithNativeCore(string folder)
    {
        using var client = new NativeWeftlineClient();
        client.LoadFolder(folder, new Dictionary<string, string>(StringComparer.Ordinal));
        return client.Compile(allowPartial: true, buildProjection: true);
    }

    private static Control BuildError(NativeProjectResponse response)
        => BuildError(
            response.Error
            ?? (response.Diagnostics.Count == 0
                ? "No graph was generated."
                : string.Join(
                    Environment.NewLine,
                    response.Diagnostics
                        .Take(20)
                        .Select(item => $"{item.Code}: {item.Message}"))));

    private static Control BuildError(string message)
        => new TextBlock
        {
            Text = message,
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#C65B5B")),
        };
}
