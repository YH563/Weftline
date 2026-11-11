using Avalonia.Media;

namespace Weftline.Graph.Rendering;

/// <summary>
/// 剧情图的默认视觉主题：浅色画布、蓝图式端口与连线。
/// Default visual theme for the story graph: light canvas with Blueprint-like
/// ports and wires. A theme service can replace it later.
/// </summary>
public static class GraphTheme
{
    public static readonly FontFamily UiFontFamily = new("Inter, Segoe UI, sans-serif");
    public static readonly Typeface Typeface = new(UiFontFamily);
    public static readonly Typeface BoldTypeface = new(UiFontFamily, FontStyle.Normal, FontWeight.SemiBold);

    public static readonly IBrush CanvasBackground = new SolidColorBrush(Color.Parse("#F6F8FC"));
    public static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.Parse("#E5EAF3")), 1);

    public static readonly IBrush ChapterFill = new SolidColorBrush(Color.Parse("#F0F4FA"));
    public static readonly IBrush ChapterHeaderFill = new SolidColorBrush(Color.Parse("#E4EBF7"));
    public static readonly IBrush ChapterEntryHeaderFill = new SolidColorBrush(Color.Parse("#E5F5EE"));
    public static readonly IBrush ChapterBorder = new SolidColorBrush(Color.Parse("#CFD8E6"));
    public static readonly IBrush ChapterTitle = new SolidColorBrush(Color.Parse("#34405A"));
    public static readonly IBrush ChapterShadow = new SolidColorBrush(Color.Parse("#10000000"));
    public static readonly IPen ChapterPen = new Pen(ChapterBorder, 1);

    public static readonly IBrush ModuleFill = new SolidColorBrush(Color.Parse("#FFFFFF"));
    public static readonly IBrush ModuleBorder = new SolidColorBrush(Color.Parse("#C7D2E2"));
    public static readonly IBrush ModuleShadow = new SolidColorBrush(Color.Parse("#16000000"));
    public static readonly IBrush ModuleTitle = new SolidColorBrush(Color.Parse("#23304A"));
    public static readonly IPen ModulePen = new Pen(ModuleBorder, 1);
    public static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.Parse("#9DB4FF")), 1.5);
    public static readonly IPen SelectedPen = new Pen(new SolidColorBrush(Color.Parse("#5B7CFA")), 2);
    public static readonly IBrush ResizeHandleFill = new SolidColorBrush(Color.Parse("#DCE5FF"));
    public static readonly IPen UnreachablePen = new Pen(
        new SolidColorBrush(Color.Parse("#C8873A")),
        1.2,
        new DashStyle(new double[] { 3, 2 }, 0));

    public static readonly IBrush EntryAccent = new SolidColorBrush(Color.Parse("#3FA37A"));
    public static readonly IBrush ExitAccent = new SolidColorBrush(Color.Parse("#5B7CFA"));
    public static readonly IPen EntryWirePen = new Pen(EntryAccent, 1.8);
    public static readonly IPen ExitWirePen = new Pen(ExitAccent, 1.8);
    public static readonly IPen ChapterAnchorPen = new Pen(Brushes.White, 1.6);

    public static readonly IBrush Wire = new SolidColorBrush(Color.Parse("#8290A6"));
    public static readonly IBrush WireGlow = new SolidColorBrush(Color.Parse("#268290A6"));
    public static readonly IBrush ChapterWire = new SolidColorBrush(Color.Parse("#5B7CFA"));
    public static readonly IBrush ChapterWireGlow = new SolidColorBrush(Color.Parse("#2E5B7CFA"));
    public static readonly IBrush Accent = ChapterWire;
    public static readonly IBrush AccentSoft = new SolidColorBrush(Color.Parse("#B7C6FF"));
    public static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#C8873A"));
    public static readonly IBrush Danger = new SolidColorBrush(Color.Parse("#C65B5B"));
    public static readonly IBrush BadgeText = Brushes.White;

    public static readonly IBrush PathColor = new SolidColorBrush(Color.Parse("#2F9E74"));
    public static readonly IBrush PathGlow = new SolidColorBrush(Color.Parse("#332F9E74"));
    public static readonly IPen PathPen = new Pen(PathColor, 2.6);
    public static readonly IPen PathGlowPen = new Pen(PathGlow, 8);
    public static readonly IPen PathModulePen = new Pen(PathColor, 2.2);

    public static readonly IPen ModuleWirePen = new Pen(Wire, 1.9);
    public static readonly IPen ModuleWireGlowPen = new Pen(WireGlow, 7);
    public static readonly IPen ChapterWirePen = new Pen(ChapterWire, 2.8);
    public static readonly IPen ChapterWireGlowPen = new Pen(ChapterWireGlow, 10);
    public static readonly IPen BackEdgePen = new Pen(
        Warning,
        2.0,
        new DashStyle(new double[] { 5, 4 }, 0));

    public static readonly IBrush EndFill = new SolidColorBrush(Color.Parse("#E8EEF9"));
    public static readonly IBrush EndBorder = new SolidColorBrush(Color.Parse("#B7C5DE"));
    public static readonly IBrush EndText = new SolidColorBrush(Color.Parse("#4C5A73"));
    public static readonly IPen EndPen = new Pen(EndBorder, 1);
}
