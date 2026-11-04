using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Weftline.App.Views;

public sealed class MessageWindow : Window
{
    public MessageWindow(string title, string message, bool showCancel)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var ok = new Button
        {
            Content = "确定",
            MinWidth = 72,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var cancel = new Button
        {
            Content = "取消",
            MinWidth = 72,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsVisible = showCancel,
        };

        ok.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 16,
        };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(buttons);

        Content = panel;
        Opened += (_, _) => ok.Focus();
    }
}
