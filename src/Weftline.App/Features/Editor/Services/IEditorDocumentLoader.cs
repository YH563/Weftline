using System.Text;

namespace Weftline.App.Features.Editor.Services;

/// <summary>
/// 文件文本的编码信息；保存时按原编码和 BOM 策略写回。
/// Encoding metadata for a text file; saves reuse the original encoding and BOM policy.
/// </summary>
public sealed record EditorFileFormat(Encoding Encoding, bool EmitBom)
{
    public static EditorFileFormat Utf8NoBom { get; } = new(new UTF8Encoding(false), false);
}

/// <summary>
/// 一次读取到的文本及其编码信息。
/// Text read from disk together with its encoding metadata.
/// </summary>
public sealed record EditorFileContent(string Text, EditorFileFormat Format);

public interface IEditorDocumentLoader
{
    EditorFileContent Read(string path);

    Task WriteAsync(
        string path,
        string text,
        EditorFileFormat format,
        CancellationToken cancellationToken = default);
}
