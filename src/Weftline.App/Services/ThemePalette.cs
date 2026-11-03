using Avalonia.Media;
using Avalonia.Styling;

namespace Weftline.App.Services;

public sealed record ThemePalette(
    bool IsDark,
    IBrush EditorBackground,
    IBrush EditorForeground,
    IBrush EditorLineNumber,
    IBrush Directive,
    IBrush Comment,
    IBrush Reference,
    IBrush String,
    IBrush GraphAccent,
    IBrush GraphDanger)
{
    public static ThemePalette Light { get; } = new(
        false,
        Brush("#FFFFFF"),
        Brush("#1F2328"),
        Brush("#8C959F"),
        Brush("#0550AE"),
        Brush("#6E7781"),
        Brush("#0F766E"),
        Brush("#953800"),
        Brush("#2563EB"),
        Brush("#DC2626"));

    public static ThemePalette Dark { get; } = new(
        true,
        Brush("#1E1F22"),
        Brush("#E6E6E6"),
        Brush("#7A7F87"),
        Brush("#82AAFF"),
        Brush("#7A7F87"),
        Brush("#4EC9B0"),
        Brush("#CE9178"),
        Brush("#7C9CFF"),
        Brush("#F87171"));

    public static ThemePalette For(ThemeVariant variant)
        => variant == ThemeVariant.Dark ? Dark : Light;

    private static IBrush Brush(string color)
        => new SolidColorBrush(Color.Parse(color));
}
