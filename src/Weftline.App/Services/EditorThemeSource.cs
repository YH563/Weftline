using Avalonia.Media;
using Weftline.App.Features.Editor.Rendering;
using Weftline.App.Features.Editor.Services;

namespace Weftline.App.Services;

public sealed class EditorThemeSource : IEditorThemeSource
{
    public EditorThemeSource()
    {
        Current = Map(ThemeService.Current);
        ThemeService.Changed += OnThemeServiceChanged;
    }

    public EditorTheme Current { get; private set; }

    public event EventHandler? Changed;

    private void OnThemeServiceChanged(object? sender, EventArgs e)
    {
        Current = Map(ThemeService.Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static EditorTheme Map(ThemePalette palette)
        => new(
            palette.IsDark,
            palette.EditorBackground,
            palette.EditorForeground,
            palette.EditorLineNumber,
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#1B2027" : "#F5F6F8")),
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#1D2430" : "#F4F8FF")),
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#2D3A4F" : "#E3ECFA")),
            palette.GraphDanger,
            new SolidColorBrush(Color.Parse("#D97706")),
            palette.GraphAccent,
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#2D4A73" : "#D6E4FF")),
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#F1F5FB" : "#1F2937")),
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#4F78B8" : "#A8C7FA")),
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#343C48" : "#D8DCE3")));
}
