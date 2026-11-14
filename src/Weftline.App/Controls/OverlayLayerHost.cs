using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Weftline.App.Controls;

/// <summary>
/// 为普通 Window 提供 PopupOverlayLayer，让 Popup 在窗口内部渲染。
///
/// SukiWindow 的模板没有 PART_VisualLayerManager；普通 Window 也不一定需要。
/// Linux 合成器对透明子窗口支持不一致，圆角弹层必须走 overlay 层，避免黑角/白角。
/// </summary>
public static class OverlayLayerHost
{
    private static readonly PropertyInfo? EnablePopupOverlayProperty =
        typeof(VisualLayerManager).GetProperty(
            "EnablePopupOverlayLayer",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Ensure(Window window)
    {
        if (window.Content is not Control content)
        {
            return;
        }

        if (content is Grid existing &&
            existing.Children.OfType<VisualLayerManager>().Any())
        {
            return;
        }

        var manager = new VisualLayerManager
        {
            EnableOverlayLayer = true,
        };

        EnablePopupOverlayProperty?.SetValue(manager, true);

        var host = new Grid();
        host.Children.Add(content);
        host.Children.Add(manager);
        window.Content = host;
    }
}
