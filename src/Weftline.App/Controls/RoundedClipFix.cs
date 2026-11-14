using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;

namespace Weftline.App.Controls;

/// <summary>
/// Linux 下禁用圆角控件的内容裁剪。
///
/// Avalonia 的 Border/ContentPresenter 在 ClipToBounds + CornerRadius 时会走圆角裁剪层；
/// 部分 X11/Wayland 合成器会把透明角渲染成黑色。这里在 Linux 上统一关闭这类裁剪，
/// 圆角背景仍由 Border/ContentPresenter 自己绘制，避免四角黑块。
/// </summary>
public static class RoundedClipFix
{
    private static bool _enabled;

    public static void Enable()
    {
        if (_enabled)
        {
            return;
        }

        _enabled = true;

        Border.ClipToBoundsProperty.Changed.AddClassHandler<Border>(
            static (border, _) => DisableClip(border));
        Border.CornerRadiusProperty.Changed.AddClassHandler<Border>(
            static (border, _) => DisableClip(border));

        ContentPresenter.ClipToBoundsProperty.Changed.AddClassHandler<ContentPresenter>(
            static (presenter, _) => DisableClip(presenter));
        ContentPresenter.CornerRadiusProperty.Changed.AddClassHandler<ContentPresenter>(
            static (presenter, _) => DisableClip(presenter));
    }

    private static void DisableClip(Border border)
    {
        if (border.ClipToBounds && border.CornerRadius != default)
        {
            border.ClipToBounds = false;
        }
    }

    private static void DisableClip(ContentPresenter presenter)
    {
        if (presenter.ClipToBounds && presenter.CornerRadius != default)
        {
            presenter.ClipToBounds = false;
        }
    }
}
