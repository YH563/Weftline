namespace Weftline.Models;

public sealed record WeftCompletionContext(
    string FilePath,
    string Text,
    int Offset,
    int WordStart);

public sealed record WeftCompletionItem(
    string Text,
    string? Label = null,
    string? Description = null,
    double Priority = 0);

public sealed record WeftCompletionResult(
    IReadOnlyList<WeftCompletionItem> Items,
    int ReplacementStart);
