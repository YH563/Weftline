namespace Weftline.Models;

public sealed record WeftSymbolInfo(
    string Name,
    bool? Initial,
    string? Description,
    SourceLocation Source);

public sealed record WeftChapterInfo(
    string Id,
    string Title,
    string? Description,
    IReadOnlyList<string> ModuleIds,
    SourceLocation Source);

public sealed record WeftProjectSymbols(
    IReadOnlyList<WeftSymbolInfo> Variables,
    IReadOnlyList<WeftSymbolInfo> Characters,
    IReadOnlyList<WeftSymbolInfo> Items,
    IReadOnlyList<WeftSymbolInfo> Scenes,
    IReadOnlyList<WeftChapterInfo> Chapters);
