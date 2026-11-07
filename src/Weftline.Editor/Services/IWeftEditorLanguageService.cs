using Weftline.Models;

namespace Weftline.Editor.Services;

public interface IWeftEditorLanguageService
{
    ValueTask<IReadOnlyList<WeftDiagnostic>> GetSyntaxDiagnosticsAsync(
        string filePath,
        string text,
        CancellationToken cancellationToken);

    ValueTask<WeftHoverInfo?> GetHoverAsync(
        WeftHoverContext context,
        CancellationToken cancellationToken);

    ValueTask<WeftCompletionResult> GetCompletionsAsync(
        WeftCompletionContext context,
        CancellationToken cancellationToken);
}
