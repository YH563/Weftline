namespace Weftline.Models;

public sealed record WeftHoverContext(
    string FilePath,
    string Text,
    int Offset);

public sealed record WeftHoverInfo(
    string Title,
    string? Detail = null,
    string? Location = null);
