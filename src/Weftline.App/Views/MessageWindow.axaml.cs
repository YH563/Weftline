using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Weftline.App.Views;

/// <summary>
/// 通用确认/提示对话框。
/// Generic confirmation and message dialog.
/// </summary>
public sealed partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message, bool showCancel)
        : this()
    {
        Title = title;
        MessageText.Text = message;
        CancelButton.IsVisible = showCancel;
        Opened += (_, _) => OkButton.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
        => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e)
        => Close(false);
}
