namespace Weftline.Core.Language;

public interface IWeftEditorLanguageService
{
    ValueTask<IReadOnlyList<WeftDiagnostic>> GetSyntaxDiagnosticsAsync(
        string filePath,
        string text,
        long version,
        CancellationToken cancellationToken);

    ValueTask<WeftHoverInfo?> GetHoverAsync(
        WeftHoverContext context,
        CancellationToken cancellationToken);

    ValueTask<WeftCompletionResult> GetCompletionsAsync(
        WeftCompletionContext context,
        CancellationToken cancellationToken);
}
