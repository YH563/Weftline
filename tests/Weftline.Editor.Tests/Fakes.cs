using Avalonia.Media;
using Weftline.Editor.Models;
using Weftline.Editor.Rendering;
using Weftline.Editor.Services;
using Weftline.Models;

namespace Weftline.Editor.Tests;

internal sealed class FakeDocumentLoader : IEditorDocumentLoader
{
    public Dictionary<string, string> Files { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string ReadAllText(string path)
        => Files.TryGetValue(path, out var text)
            ? text
            : throw new FileNotFoundException(path);

    public void WriteAllText(string path, string text) => Files[path] = text;

    public Task<string> ReadAllTextAsync(
        string path,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ReadAllText(path));

    public Task WriteAllTextAsync(
        string path,
        string text,
        CancellationToken cancellationToken = default)
    {
        WriteAllText(path, text);
        return Task.CompletedTask;
    }
}

internal sealed class FakeIconProvider : IEditorIconProvider
{
    public EditorIcon Resolve(string name, bool isDirectory, bool isExpanded)
        => new(new StreamGeometry(), Brushes.Transparent);
}

internal sealed class FakeThemeSource : IEditorThemeSource
{
    public EditorTheme Current { get; } = EditorTheme.Default;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}

internal sealed class FakeLanguageService : IWeftEditorLanguageService
{
    public Func<string, string, long, IReadOnlyList<WeftDiagnostic>>? SyntaxHandler { get; set; }

    public string? LastFilePath { get; private set; }

    public long LastVersion { get; private set; }

    public ValueTask<IReadOnlyList<WeftDiagnostic>> GetSyntaxDiagnosticsAsync(
        string filePath,
        string text,
        long version,
        CancellationToken cancellationToken)
    {
        LastFilePath = filePath;
        LastVersion = version;
        var result = SyntaxHandler?.Invoke(filePath, text, version) ?? [];
        return ValueTask.FromResult(result);
    }

    public ValueTask<WeftHoverInfo?> GetHoverAsync(
        WeftHoverContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult<WeftHoverInfo?>(null);

    public ValueTask<WeftCompletionResult> GetCompletionsAsync(
        WeftCompletionContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(new WeftCompletionResult([], context.WordStart));
}
