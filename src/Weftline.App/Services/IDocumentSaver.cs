using System.Text;

namespace Weftline.App.Services;

/// <summary>
/// 文档落盘端口：ViewModel 只负责决定"要保存什么"，写盘由实现负责。
/// </summary>
public interface IDocumentSaver
{
    Task SaveAsync(string path, string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认实现：保留原文件编码与 BOM，并通过同目录临时文件原子替换。
/// Default implementation: keeps the existing encoding/BOM and replaces the file
/// atomically through a sibling temp file.
/// </summary>
public sealed class FileDocumentSaver : IDocumentSaver
{
    public async Task SaveAsync(
        string path,
        string text,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new IOException($"无法确定目录：{path}");
        var (encoding, emitBom) = DetectFormat(fullPath);
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
                if (emitBom)
                {
                    await stream
                        .WriteAsync(encoding.GetPreamble(), cancellationToken)
                        .ConfigureAwait(false);
                }

                var bytes = encoding.GetBytes(text);
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

    private static (Encoding Encoding, bool EmitBom) DetectFormat(string path)
    {
        if (!File.Exists(path))
        {
            return (new UTF8Encoding(false), false);
        }

        Span<byte> bytes = stackalloc byte[4];
        var length = 0;

        using (var stream = File.OpenRead(path))
        {
            length = stream.Read(bytes);
        }

        // UTF-32 的 BOM 以 UTF-16 的前缀开头，必须先判断。
        if (length >= 4)
        {
            if (bytes[..4].SequenceEqual(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
            {
                return (new UTF32Encoding(bigEndian: false, byteOrderMark: true), true);
            }

            if (bytes[..4].SequenceEqual(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
            {
                return (new UTF32Encoding(bigEndian: true, byteOrderMark: true), true);
            }
        }

        if (length >= 3 && bytes[..3].SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return (new UTF8Encoding(true), true);
        }

        if (length >= 2)
        {
            if (bytes[..2].SequenceEqual(new byte[] { 0xFF, 0xFE }))
            {
                return (new UnicodeEncoding(bigEndian: false, byteOrderMark: true), true);
            }

            if (bytes[..2].SequenceEqual(new byte[] { 0xFE, 0xFF }))
            {
                return (new UnicodeEncoding(bigEndian: true, byteOrderMark: true), true);
            }
        }

        return (new UTF8Encoding(false), false);
    }
}
