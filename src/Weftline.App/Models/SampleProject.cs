namespace Weftline.App.Models;

public sealed record SampleModule(
    string Id,
    string Scene,
    string Summary);

public sealed record SampleChapter(
    string Title,
    IReadOnlyList<SampleModule> Modules);

public sealed record SampleEdge(
    string From,
    string To,
    string? Label,
    string? Condition,
    bool External);

public sealed record SampleProblem(
    string Severity,
    string Code,
    string Location,
    string Message);

public static class SampleProject
{
    public const string Name = "雨夜";

    public static IReadOnlyList<SampleChapter> Chapters { get; } =
    [
        new SampleChapter("第一章 雨夜",
        [
            new SampleModule("序", "街道", "萧影走进雨夜。"),
            new SampleModule("相信", "医院", "选择是否相信影子。"),
            new SampleModule("医院", "医院", "医院大厅。"),
            new SampleModule("沉默", "医院", "萧影保持沉默。"),
        ]),
        new SampleChapter("第二章 病房",
        [
            new SampleModule("病房", "病房", "病房里很安静。"),
            new SampleModule("走廊", "走廊", "走廊尽头。"),
        ]),
    ];

    public static IReadOnlyList<SampleEdge> Edges { get; } =
    [
        new SampleEdge("序", "相信", null, null, false),
        new SampleEdge("相信", "医院", "相信他", "trust_low", false),
        new SampleEdge("相信", "沉默", "沉默", null, false),
        new SampleEdge("医院", "病房", null, "has_key", true),
        new SampleEdge("医院", "走廊", null, null, true),
        new SampleEdge("病房", "走廊", null, null, false),
    ];

    public static IReadOnlyList<SampleProblem> Problems { get; } =
    [
        new SampleProblem("warning", "WFT4004", "chapter1.weft:18:5", "模块从入口不可达：沉默"),
        new SampleProblem("info", "WFT4008", "chapter1.weft:2:1", "变量 has_key 已声明但未使用"),
        new SampleProblem("error", "WFT3006", "chapter1.weft:24:12", "条件引用了未声明变量：trust_high"),
    ];

    public const string WeftSample = """
    @title 第一章 雨夜

    @vars
        @var trust_low false
        @var has_key false

    @chars
        @char 萧影
        @char 影子

    @items
        @item 医院钥匙

    @scenes
        @scene 街道
        @scene 医院

    @module 序
        @scene 街道
        [雨夜，{萧影}走向医院。]
    @next -> 相信 { @set trust_low true }

    @module 相信
        @scene 医院
        [雨水模糊了视线，{萧影}在泥泞中奔跑。]
        萧影:[你来了。]
        影子:[别回头。]
    @next(萧影):(trust_low)[相信他] -> 医院 { @set has_key true }
        ()[沉默] -> 沉默

    @module 医院
        @scene 医院
        [两人走进医院。]
    @next():(has_key) -> 病房
        () -> 走廊

    @module 沉默
        @scene 医院
        [萧影没有说话。]
    @next -> END
    """;
}
