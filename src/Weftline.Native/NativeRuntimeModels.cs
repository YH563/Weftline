using Weftline.Models;
using Weftline.Player;

namespace Weftline.Native;

/// <summary>
/// 播放器载荷响应。
/// </summary>
public sealed class NativeRuntimeResponse
{
    public int SchemaVersion { get; set; }
    public bool HasErrors { get; set; }
    public string? Error { get; set; }
    public int DiagnosticCount { get; set; }
    public NativeRuntimeProjection? Runtime { get; set; }
}

/// <summary>
/// native 播放器载荷的 JSON DTO。
/// </summary>
public sealed class NativeRuntimeProjection
{
    public int SchemaVersion { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? Entry { get; set; }
    public string End { get; set; } = "END";
    public List<NativeRuntimeChapter> Chapters { get; set; } = [];
    public List<NativeRuntimeGlobal> Globals { get; set; } = [];
    public List<NativeRuntimeModule> Modules { get; set; } = [];

    /// <summary>
    /// 转换为播放器领域模型。
    /// </summary>
    public RuntimeProjection ToRuntimeProjection()
        => new(
            SchemaVersion,
            ProjectName,
            Entry,
            End,
            Chapters.Select(static chapter => chapter.ToRuntimeChapter()).ToList(),
            Globals.Select(static global => global.ToRuntimeGlobal()).ToList(),
            Modules.Select(static module => module.ToRuntimeModule()).ToList());
}

/// <summary>
/// 播放器章节 DTO。
/// </summary>
public sealed class NativeRuntimeChapter
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public RuntimeChapter ToRuntimeChapter() => new(Id, Title);
}

/// <summary>
/// 全局变量 DTO。
/// </summary>
public sealed class NativeRuntimeGlobal
{
    public string Name { get; set; } = string.Empty;
    public bool Initial { get; set; }

    public RuntimeGlobal ToRuntimeGlobal() => new(Name, Initial);
}

/// <summary>
/// 播放器模块 DTO。
/// </summary>
public sealed class NativeRuntimeModule
{
    public string Id { get; set; } = string.Empty;
    public string ChapterId { get; set; } = string.Empty;
    public List<NativeRuntimeScene> Scenes { get; set; } = [];
    public List<NativeRuntimeContent> Content { get; set; } = [];
    public NativeRuntimeExit? Exit { get; set; }

    public RuntimeModule ToRuntimeModule()
        => new(
            Id,
            ChapterId,
            Scenes.Select(static scene => new RuntimeScene(scene.Name, scene.ContentIndex)).ToList(),
            Content.Select(static content => content.ToRuntimeContent()).ToList(),
            Exit?.ToRuntimeExit());
}

/// <summary>
/// 模块内场景标记 DTO。
/// </summary>
public sealed class NativeRuntimeScene
{
    public string Name { get; set; } = string.Empty;
    public int ContentIndex { get; set; }
}

/// <summary>
/// 正文块 DTO。
/// </summary>
public sealed class NativeRuntimeContent
{
    public string Kind { get; set; } = "narration";
    public List<NativeRuntimeSegment> Segments { get; set; } = [];
    public string? Speaker { get; set; }
    public string? Path { get; set; }

    public RuntimeContent ToRuntimeContent()
    {
        var kind = Kind switch
        {
            "dialogue" => RuntimeContentKind.Dialogue,
            "figure" => RuntimeContentKind.Figure,
            _ => RuntimeContentKind.Narration,
        };

        return new RuntimeContent(
            kind,
            Segments.Select(static segment => segment.ToRuntimeSegment()).ToList(),
            Speaker,
            Path);
    }
}

/// <summary>
/// 正文片段 DTO；引用片段的 Name 会映射为显示文本。
/// </summary>
public sealed class NativeRuntimeSegment
{
    public string Kind { get; set; } = "text";
    public string? Text { get; set; }
    public string? Name { get; set; }

    public RuntimeTextSegment ToRuntimeSegment()
        => string.Equals(Kind, "ref", StringComparison.OrdinalIgnoreCase)
            ? new RuntimeTextSegment(RuntimeTextSegmentKind.Reference, Name ?? string.Empty)
            : new RuntimeTextSegment(RuntimeTextSegmentKind.Text, Text ?? string.Empty);
}

/// <summary>
/// 出口块 DTO。
/// </summary>
public sealed class NativeRuntimeExit
{
    public string Mode { get; set; } = "auto";
    public string? Character { get; set; }
    public List<NativeRuntimeBranch> Branches { get; set; } = [];

    public RuntimeExit ToRuntimeExit()
        => new(
            string.Equals(Mode, "choice", StringComparison.OrdinalIgnoreCase)
                ? ExitMode.Choice
                : ExitMode.Auto,
            Character,
            Branches.Select(static branch => branch.ToRuntimeBranch()).ToList());
}

/// <summary>
/// 出口分支 DTO。
/// </summary>
public sealed class NativeRuntimeBranch
{
    public NativeBoolExpr? Condition { get; set; }
    public string? Label { get; set; }
    public string Target { get; set; } = string.Empty;
    public List<NativeRuntimeEffect> Effects { get; set; } = [];

    public RuntimeBranch ToRuntimeBranch()
        => new(
            NativeProjection.ToBoolExpr(Condition),
            Label,
            Target,
            Effects.Select(static effect => effect.ToRuntimeEffect()).ToList());
}

/// <summary>
/// 分支效果 DTO。
/// </summary>
public sealed class NativeRuntimeEffect
{
    public string Name { get; set; } = string.Empty;
    public bool Value { get; set; }

    public RuntimeEffect ToRuntimeEffect() => new(Name, Value);
}
