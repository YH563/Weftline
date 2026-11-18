using Avalonia.Media;
using Weftline.App.Features.Editor.Models;
using Weftline.App.Features.Editor.Rendering;
using Weftline.App.Features.Editor.Services;
using Weftline.Core;

namespace Weftline.App.Tests;

internal sealed class FakeDocumentLoader : IEditorDocumentLoader
{
    public Dictionary<string, string> Files { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, EditorFileFormat> Formats { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public EditorFileContent Read(string path)
    {
        if (!Files.TryGetValue(path, out var text))
        {
            throw new FileNotFoundException(path);
        }

        var format = Formats.TryGetValue(path, out var existing)
            ? existing
            : EditorFileFormat.Utf8NoBom;

        return new EditorFileContent(text, format);
    }

    public Task WriteAsync(
        string path,
        string text,
        EditorFileFormat format,
        CancellationToken cancellationToken = default)
    {
        Files[path] = text;
        Formats[path] = format;
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
