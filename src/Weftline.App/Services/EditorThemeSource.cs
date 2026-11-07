using Avalonia.Media;
using Weftline.Editor.Rendering;
using Weftline.Editor.Services;

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
            new SolidColorBrush(Color.Parse("#F5F6F8")),
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#1D2430" : "#F4F8FF")),
            new SolidColorBrush(Color.Parse(palette.IsDark ? "#2D3A4F" : "#E3ECFA")),
            palette.Directive,
            palette.Comment,
            palette.Reference,
            palette.String,
            palette.Directive,
            palette.GraphDanger,
            new SolidColorBrush(Color.Parse("#D97706")),
            palette.GraphAccent);
}
