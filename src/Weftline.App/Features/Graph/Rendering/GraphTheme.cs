using Avalonia.Media;
using Weftline.App.Services;

namespace Weftline.App.Features.Graph.Rendering;

/// <summary>
/// 剧情图的视觉主题：跟随应用浅色/深色主题切换。
/// Visual theme for the story graph, following the app light/dark theme.
/// </summary>
public static class GraphTheme
{
    // 直接选一个带 CJK 的系统字体，避免 Linux/Skia 在 FormattedText 中
    // 逐字回退到中文字体时出现严重的渲染卡顿。
    // Pick a CJK-capable system font directly; per-glyph fallback in
    // FormattedText can be extremely slow on Linux/Skia.
    public static readonly FontFamily UiFontFamily = ResolveUiFontFamily();
    public static readonly Typeface Typeface = new(UiFontFamily);
    public static readonly Typeface BoldTypeface = new(UiFontFamily, FontStyle.Normal, FontWeight.SemiBold);

    static GraphTheme()
    {
        Apply(ThemeService.Current);
        ThemeService.Changed += (_, _) => Apply(ThemeService.Current);
    }

    public static IBrush CanvasBackground { get; private set; } = Brushes.White;
    public static IPen GridPen { get; private set; } = new Pen(Brushes.Gray, 1);

    public static IBrush ChapterFill { get; private set; } = Brushes.White;
    public static IBrush ChapterHeaderFill { get; private set; } = Brushes.White;
    public static IBrush ChapterEntryHeaderFill { get; private set; } = Brushes.White;
    public static IBrush ChapterBorder { get; private set; } = Brushes.Gray;
    public static IBrush ChapterTitle { get; private set; } = Brushes.Black;
    public static IBrush ChapterShadow { get; private set; } = Brushes.Transparent;
    public static IPen ChapterPen { get; private set; } = new Pen(Brushes.Gray, 1);

    public static IBrush ModuleFill { get; private set; } = Brushes.White;
    public static IBrush ModuleBorder { get; private set; } = Brushes.Gray;
    public static IBrush ModuleShadow { get; private set; } = Brushes.Transparent;
    public static IBrush ModuleTitle { get; private set; } = Brushes.Black;
    public static IPen ModulePen { get; private set; } = new Pen(Brushes.Gray, 1);
    public static IPen HoverPen { get; private set; } = new Pen(Brushes.Blue, 1.5);
    public static IBrush SelectionAccent { get; private set; } = Brushes.Purple;
    public static IPen SelectedPen { get; private set; } = new Pen(Brushes.Purple, 2.8);
    public static IBrush ResizeHandleFill { get; private set; } = Brushes.LightBlue;
    public static IPen UnreachablePen { get; private set; } = new Pen(Brushes.Orange, 1.2);

    public static IBrush EntryAccent { get; private set; } = Brushes.Green;
    public static IBrush ExitAccent { get; private set; } = Brushes.Blue;
    public static IPen EntryWirePen { get; private set; } = new Pen(Brushes.Green, 1.8);
    public static IPen EntryModulePen { get; private set; } = new Pen(Brushes.Green, 1.8);
    public static IPen EntryCandidatePen { get; private set; } = new Pen(Brushes.Green, 1.2);
    public static IPen ExitWirePen { get; private set; } = new Pen(Brushes.Blue, 1.8);
    public static IPen ChapterAnchorPen { get; private set; } = new Pen(Brushes.White, 1.6);

    public static IBrush Wire { get; private set; } = Brushes.Gray;
    public static IBrush WireGlow { get; private set; } = Brushes.Transparent;
    public static IBrush ChapterWire { get; private set; } = Brushes.Blue;
    public static IBrush ChapterWireGlow { get; private set; } = Brushes.Transparent;
    public static IBrush Accent { get; private set; } = Brushes.Blue;
    public static IBrush AccentSoft { get; private set; } = Brushes.LightBlue;
    public static IBrush Warning { get; private set; } = Brushes.Orange;
    public static IBrush Danger { get; private set; } = Brushes.Red;
    public static IBrush BadgeText { get; private set; } = Brushes.White;

    public static IBrush PathColor { get; private set; } = Brushes.Purple;
    public static IBrush PathGlow { get; private set; } = Brushes.Transparent;
    public static IPen PathPen { get; private set; } = new Pen(Brushes.Purple, 2.6);
    public static IPen PathGlowPen { get; private set; } = new Pen(Brushes.Transparent, 8);
    public static IPen PathModulePen { get; private set; } = new Pen(Brushes.Purple, 2.2);

    public static IPen ModuleWirePen { get; private set; } = new Pen(Brushes.Gray, 1.9);
    public static IPen ModuleWireGlowPen { get; private set; } = new Pen(Brushes.Transparent, 7);
    public static IPen ChapterWirePen { get; private set; } = new Pen(Brushes.Blue, 2.8);
    public static IPen ChapterWireGlowPen { get; private set; } = new Pen(Brushes.Transparent, 10);
    public static IPen BackEdgePen { get; private set; } = new Pen(Brushes.Orange, 2.0);

    public static IBrush EndFill { get; private set; } = Brushes.White;
    public static IBrush EndBorder { get; private set; } = Brushes.Gray;
    public static IBrush EndText { get; private set; } = Brushes.Black;
    public static IPen EndPen { get; private set; } = new Pen(Brushes.Gray, 1);

    private static void Apply(ThemePalette palette)
    {
        var dark = palette.IsDark;

        CanvasBackground = Brush(dark ? "#1B2027" : "#F6F8FC");
        GridPen = new Pen(Brush(dark ? "#2A323D" : "#E5EAF3"), 1);

        ChapterFill = Brush(dark ? "#20262F" : "#F0F4FA");
        ChapterHeaderFill = Brush(dark ? "#26303C" : "#E4EBF7");
        ChapterEntryHeaderFill = Brush(dark ? "#1F332C" : "#E5F5EE");
        ChapterBorder = Brush(dark ? "#39424F" : "#D5DEEA");
        ChapterTitle = Brush(dark ? "#D7E1EE" : "#34405A");
        ChapterShadow = Brush(dark ? "#40000000" : "#0C000000");
        ChapterPen = new Pen(ChapterBorder, 1);

        ModuleFill = Brush(dark ? "#252C36" : "#FFFFFF");
        ModuleBorder = Brush(dark ? "#3B4655" : "#D2DAE6");
        ModuleShadow = Brush(dark ? "#40000000" : "#0D000000");
        ModuleTitle = Brush(dark ? "#E1E8F2" : "#23304A");
        ModulePen = new Pen(ModuleBorder, 1);
        HoverPen = new Pen(Brush(dark ? "#7C9CFF" : "#9DB4FF"), 1.5);
        SelectionAccent = Brush(dark ? "#B49CFF" : "#8B5CF6");
        SelectedPen = new Pen(SelectionAccent, 2.8);
        ResizeHandleFill = Brush(dark ? "#3B4A63" : "#DCE5FF");
        UnreachablePen = new Pen(
            Brush(dark ? "#D99A4E" : "#C8873A"),
            1.2,
            new DashStyle(new double[] { 3, 2 }, 0));

        EntryAccent = Brush(dark ? "#5BC49A" : "#3FA37A");
        ExitAccent = Brush(dark ? "#7C9CFF" : "#5B7CFA");
        EntryWirePen = new Pen(EntryAccent, 1.8);
        EntryModulePen = new Pen(EntryAccent, 1.8);
        EntryCandidatePen = new Pen(
            EntryAccent,
            1.2,
            new DashStyle(new double[] { 3, 2 }, 0));
        ExitWirePen = new Pen(ExitAccent, 1.8);
        ChapterAnchorPen = new Pen(Brushes.White, 1.6);

        Wire = Brush(dark ? "#8A98AD" : "#8290A6");
        WireGlow = Brush(dark ? "#338A98AD" : "#268290A6");
        ChapterWire = Brush(dark ? "#7C9CFF" : "#5B7CFA");
        ChapterWireGlow = Brush(dark ? "#407C9CFF" : "#2E5B7CFA");
        Accent = ChapterWire;
        AccentSoft = Brush(dark ? "#3B4E75" : "#B7C6FF");
        Warning = Brush(dark ? "#E0A94A" : "#C8873A");
        Danger = Brush(dark ? "#F07171" : "#C65B5B");
        BadgeText = Brushes.White;

        PathColor = SelectionAccent;
        PathGlow = Brush(dark ? "#44B49CFF" : "#338B5CF6");
        PathPen = new Pen(PathColor, 2.6);
        PathGlowPen = new Pen(PathGlow, 8);
        PathModulePen = new Pen(PathColor, 2.2);

        ModuleWirePen = new Pen(Wire, 1.9);
        ModuleWireGlowPen = new Pen(WireGlow, 7);
        ChapterWirePen = new Pen(ChapterWire, 2.8);
        ChapterWireGlowPen = new Pen(ChapterWireGlow, 10);
        BackEdgePen = new Pen(
            Warning,
            2.0,
            new DashStyle(new double[] { 5, 4 }, 0));

        EndFill = Brush(dark ? "#2A3340" : "#E8EEF9");
        EndBorder = Brush(dark ? "#4A5A70" : "#B7C5DE");
        EndText = Brush(dark ? "#C5D0DE" : "#4C5A73");
        EndPen = new Pen(EndBorder, 1);
    }

    private static IBrush Brush(string color)
        => new SolidColorBrush(Color.Parse(color));

    /// <summary>
    /// 选择当前平台存在的 CJK 字体，避免 Skia 逐字回退。
    /// Selects an installed CJK font to avoid slow per-glyph fallback.
    /// </summary>
    private static FontFamily ResolveUiFontFamily()
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC" }
            : OperatingSystem.IsMacOS()
                ? new[] { "PingFang SC", "Hiragino Sans GB", "Noto Sans CJK SC" }
                : new[] { "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "WenQuanYi Micro Hei" };

        foreach (var name in candidates)
        {
            var family = new FontFamily(name);

            if (FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var glyph) &&
                glyph.CharacterToGlyphMap.TryGetGlyph('你', out _))
            {
                return family;
            }
        }

        return FontManager.Current.DefaultFontFamily;
    }
}
