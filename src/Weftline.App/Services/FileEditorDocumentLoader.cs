using System.Text;
using Weftline.App.Features.Editor.Services;

namespace Weftline.App.Services;

public sealed class FileEditorDocumentLoader : IEditorDocumentLoader
{
    public EditorFileContent Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (encoding, bomLength, emitBom) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
        return new EditorFileContent(text, new EditorFileFormat(encoding, emitBom));
    }

    /// <summary>
    /// 原子写入：先写同目录临时文件，再替换目标文件，避免崩溃时截断原文件。
    /// Atomic write: write a sibling temp file first, then replace the target so a
    /// crash cannot truncate the original file.
    /// </summary>
    public async Task WriteAsync(
        string path,
        string text,
        EditorFileFormat format,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException($"无法确定目录：{path}");
        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                             tempPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 16 * 1024,
                             useAsync: true))
            {
                if (format.EmitBom)
                {
                    await stream
                        .WriteAsync(format.Encoding.GetPreamble(), cancellationToken)
                        .ConfigureAwait(false);
                }

                var bytes = format.Encoding.GetBytes(text);
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static (Encoding Encoding, int BomLength, bool EmitBom) DetectEncoding(
        ReadOnlySpan<byte> bytes)
    {
        // UTF-32 的 BOM 以 UTF-16 的前缀开头，必须先判断。
        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
        {
            return (new UTF32Encoding(bigEndian: false, byteOrderMark: true), 4, true);
        }

        if (bytes.StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
        {
            return (new UTF32Encoding(bigEndian: true, byteOrderMark: true), 4, true);
        }

        if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return (new UTF8Encoding(true), 3, true);
        }

        if (bytes.StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return (new UnicodeEncoding(bigEndian: false, byteOrderMark: true), 2, true);
        }

        if (bytes.StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return (new UnicodeEncoding(bigEndian: true, byteOrderMark: true), 2, true);
        }

        return (new UTF8Encoding(false), 0, false);
    }
}
