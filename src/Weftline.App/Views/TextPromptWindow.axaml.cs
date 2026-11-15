using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Weftline.App.Views;

/// <summary>
/// 单行文本输入对话框。
/// Single-line text prompt dialog.
/// </summary>
public sealed partial class TextPromptWindow : Window
{
    public TextPromptWindow()
    {
        InitializeComponent();
    }

    public TextPromptWindow(string title, string message, string initialValue)
        : this()
    {
        Title = title;
        MessageText.Text = message;
        InputBox.Text = initialValue;
        Opened += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
        => Close(InputBox.Text?.Trim());

    private void OnCancelClick(object? sender, RoutedEventArgs e)
        => Close(null);
}
