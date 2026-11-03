using Avalonia;
using Avalonia.Styling;

namespace Weftline.App.Services;

public static class ThemeService
{
    private static bool _initialized;

    public static ThemePalette Current { get; private set; } = ThemePalette.Light;

    public static event EventHandler? Changed;

    public static void Initialize()
    {
        if (_initialized || Application.Current is not { } app)
        {
            return;
        }

        _initialized = true;
        app.ActualThemeVariantChanged += OnActualThemeVariantChanged;
        Apply(app.ActualThemeVariant);
    }

    public static void Apply(ThemeVariant variant)
    {
        Current = ThemePalette.For(variant);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (Application.Current is { } app)
        {
            Apply(app.ActualThemeVariant);
        }
    }
}
