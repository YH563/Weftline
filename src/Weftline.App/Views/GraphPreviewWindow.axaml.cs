using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Weftline.Native;

namespace Weftline.App.Views;

/// <summary>
/// 开发用剧情图预览窗口。启动参数：--graph-preview &lt;folder&gt;。
/// Development-only story graph preview. Launch with: --graph-preview &lt;folder&gt;.
/// </summary>
public sealed partial class GraphPreviewWindow : Window
{
    public GraphPreviewWindow()
    {
        InitializeComponent();
    }

    public GraphPreviewWindow(string folder)
        : this()
    {
        Title = $"Weftline Graph Preview - {Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))}";

        NativeProjectResponse response;

        try
        {
            response = CompileWithNativeCore(folder);
        }
        catch (Exception exception)
        {
            ShowError($"Native core failed: {exception.Message}");
            return;
        }

        var projection = response.Projection?.ToGraphProjection();

        if (projection is null)
        {
            ShowError(response);
            return;
        }

        GraphView.Projection = projection;

        var projectName = string.IsNullOrWhiteSpace(response.ProjectName)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(folder))
            : response.ProjectName;

        HeaderText.Text = $"{projectName}  ·  " +
                          $"{projection.Chapters.Count} chapters  ·  " +
                          $"{projection.Modules.Count} modules  ·  " +
                          $"{projection.ChapterEdges.Count} chapter edges";
    }

    private void OnResetLayoutClick(object? sender, RoutedEventArgs e)
        => GraphView.ResetLayout();

    private static NativeProjectResponse CompileWithNativeCore(string folder)
    {
        using var client = new NativeWeftlineClient();
        client.LoadFolder(folder, new Dictionary<string, string>(StringComparer.Ordinal));
        return client.Compile(allowPartial: true, buildProjection: true);
    }

    private void ShowError(NativeProjectResponse response)
        => ShowError(
            response.Error
            ?? (response.Diagnostics.Count == 0
                ? "No graph was generated."
                : string.Join(
                    Environment.NewLine,
                    response.Diagnostics
                        .Take(20)
                        .Select(item => $"{item.Code}: {item.Message}"))));

    private void ShowError(string message)
    {
        Content = new TextBlock
        {
            Text = message,
            Margin = new Thickness(24),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#C65B5B")),
        };
    }
}
