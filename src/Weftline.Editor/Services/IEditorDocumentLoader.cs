namespace Weftline.Editor.Services;

public interface IEditorDocumentLoader
{
    string ReadAllText(string path);

    void WriteAllText(string path, string text);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllTextAsync(
        string path,
        string text,
        CancellationToken cancellationToken = default);
}
