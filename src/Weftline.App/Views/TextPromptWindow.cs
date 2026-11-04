using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Weftline.App.Views;

public sealed class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string message, string initialValue)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var textBox = new TextBox
        {
            Text = initialValue,
            Margin = new Thickness(0, 0, 0, 14),
        };

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
        };

        ok.Click += (_, _) => Close(textBox.Text?.Trim());
        cancel.Click += (_, _) => Close(null);

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
            Spacing = 10,
        };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(textBox);
        panel.Children.Add(buttons);

        Content = panel;
        Opened += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };
    }
}
