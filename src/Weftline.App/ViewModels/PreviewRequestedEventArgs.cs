namespace Weftline.App.ViewModels;

/// <summary>
/// 请求主窗口打开剧本预览。
/// </summary>
public sealed class PreviewRequestedEventArgs : EventArgs
{
    public PreviewRequestedEventArgs(PreviewViewModel viewModel)
        => ViewModel = viewModel;

    public PreviewViewModel ViewModel { get; }
}
