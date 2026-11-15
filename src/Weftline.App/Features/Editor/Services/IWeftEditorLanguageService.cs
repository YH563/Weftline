using Weftline.Core;

namespace Weftline.App.Features.Editor.Services;

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
