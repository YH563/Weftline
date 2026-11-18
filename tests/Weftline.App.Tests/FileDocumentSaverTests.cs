using System.Text;
using Weftline.App.Services;

namespace Weftline.App.Tests;

public sealed class FileDocumentSaverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "weftline-tests",
        Guid.NewGuid().ToString("N"));

    public FileDocumentSaverTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task PreservesUtf8BomAndWritesAtomically()
    {
        var path = Path.Combine(_root, "a.weft");
        File.WriteAllBytes(
            path,
            [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("@title 旧\n")]);

        await new FileDocumentSaver().SaveAsync(path, "@title 新\n");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(
            "@title 新\n",
            Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task PreservesUtf16LittleEndian()
    {
        var path = Path.Combine(_root, "b.weft");
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
        File.WriteAllBytes(path, [0xFF, 0xFE, .. encoding.GetBytes("@title 旧\n")]);

        await new FileDocumentSaver().SaveAsync(path, "@title 新\n");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("@title 新\n", encoding.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public async Task NewFileIsUtf8WithoutBom()
    {
        var path = Path.Combine(_root, "c.weft");

        await new FileDocumentSaver().SaveAsync(path, "@title 新\n");

        var bytes = File.ReadAllBytes(path);
        Assert.Equal("@title 新\n", Encoding.UTF8.GetString(bytes));
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
