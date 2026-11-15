using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Weftline.App.Controls;

/// <summary>
/// 圆角图片：用 ImageBrush 把图片画进圆角矩形，不依赖 ClipToBounds。
/// </summary>
public sealed class RoundedImage : Control
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<RoundedImage, IImage?>(nameof(Source));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<RoundedImage, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<RoundedImage, Stretch>(nameof(Stretch), Stretch.Uniform);

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<RoundedImage, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<RoundedImage, IBrush?>(nameof(BorderBrush));

    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<RoundedImage, Thickness>(nameof(BorderThickness));

    static RoundedImage()
    {
        AffectsRender<RoundedImage>(
            SourceProperty,
            CornerRadiusProperty,
            StretchProperty,
            BackgroundProperty,
            BorderBrushProperty,
            BorderThicknessProperty);
    }

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
        => Source is { } source
            ? new Size(source.Size.Width, source.Size.Height)
            : default;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var radius = Math.Max(0, CornerRadius.TopLeft);

        if (Background is { } background)
        {
            context.DrawRectangle(background, null, bounds, radius, radius);
        }

        if (Source is { } source &&
            source is IImageBrushSource brushSource &&
            source.Size.Width > 0 &&
            source.Size.Height > 0)
        {
            var destination = Stretch == Stretch.Uniform
                ? UniformRect(bounds, source.Size)
                : bounds;

            var brush = new ImageBrush
            {
                Source = brushSource,
                Stretch = Stretch == Stretch.UniformToFill
                    ? Stretch.UniformToFill
                    : Stretch.Fill,
            };

            context.DrawRectangle(brush, null, destination, radius, radius);
        }

        if (BorderBrush is { } borderBrush && BorderThickness.Left > 0)
        {
            context.DrawRectangle(
                null,
                new Pen(borderBrush, BorderThickness.Left),
                bounds,
                radius,
                radius);
        }
    }

    private static Rect UniformRect(Rect bounds, Size imageSize)
    {
        var scale = Math.Min(
            bounds.Width / imageSize.Width,
            bounds.Height / imageSize.Height);
        var width = imageSize.Width * scale;
        var height = imageSize.Height * scale;

        return new Rect(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height);
    }
}
