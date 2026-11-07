using Weftline.Editor.Services;

namespace Weftline.App.Services;

public sealed class FileEditorDocumentLoader : IEditorDocumentLoader
{
    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string text) => File.WriteAllText(path, text);

    public Task<string> ReadAllTextAsync(
        string path,
        CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(path, cancellationToken);

    public Task WriteAllTextAsync(
        string path,
        string text,
        CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, text, cancellationToken);
}
