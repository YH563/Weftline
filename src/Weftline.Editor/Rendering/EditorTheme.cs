using Avalonia.Media;

namespace Weftline.Editor.Rendering;

public sealed record EditorTheme(
    bool IsDark,
    IBrush Background,
    IBrush Foreground,
    IBrush LineNumberForeground,
    IBrush LineNumberBackground,
    IBrush CurrentLineBackground,
    IBrush CurrentLineBorder,
    IBrush DiagnosticError,
    IBrush DiagnosticWarning,
    IBrush DiagnosticInfo)
{
    public static EditorTheme Default { get; } = new(
        false,
        Brush("#FFFFFF"),
        Brush("#1F2328"),
        Brush("#8C959F"),
        Brush("#F5F6F8"),
        Brush("#F4F8FF"),
        Brush("#E3ECFA"),
        Brush("#DC2626"),
        Brush("#D97706"),
        Brush("#2563EB"));

    private static IBrush Brush(string color)
        => new SolidColorBrush(Color.Parse(color));
}
