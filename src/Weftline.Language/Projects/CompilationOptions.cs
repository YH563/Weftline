namespace Weftline.Language.Projects;

/// <summary>
/// 项目编译选项。实时预览使用 AllowPartial + BuildProjection。
/// Project compilation options. Real-time preview uses AllowPartial + BuildProjection.
/// </summary>
public sealed record CompilationOptions(
    bool AllowPartial = false,
    bool BuildProjection = false);
