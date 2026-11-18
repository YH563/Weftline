using System.Text;
using Weftline.App.Services;

namespace Weftline.App.Tests;

public sealed class FileEditorDocumentLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "weftline-tests",
        Guid.NewGuid().ToString("N"));

    public FileEditorDocumentLoaderTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task PreservesUtf8BomAndWritesAtomically()
    {
        var path = Path.Combine(_root, "a.weft");
        var text = "@title 测试\n";
        File.WriteAllBytes(
            path,
            [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)]);

        var loader = new FileEditorDocumentLoader();
        var content = loader.Read(path);

        Assert.True(content.Format.EmitBom);
        Assert.Equal(text, content.Text);

        await loader.WriteAsync(path, "@title 更新\n", content.Format);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal(
            "@title 更新\n",
            Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task PreservesUtf16LittleEndian()
    {
        var path = Path.Combine(_root, "b.weft");
        var text = "@title 测试\n";
        var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);

        File.WriteAllBytes(
            path,
            [0xFF, 0xFE, .. encoding.GetBytes(text)]);

        var loader = new FileEditorDocumentLoader();
        var content = loader.Read(path);

        Assert.True(content.Format.EmitBom);
        Assert.Equal(text, content.Text);

        await loader.WriteAsync(path, "@title 更新\n", content.Format);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal(
            "@title 更新\n",
            encoding.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void KeepsPlainUtf8WithoutBom()
    {
        var path = Path.Combine(_root, "c.weft");
        File.WriteAllText(path, "@title 无 BOM\n", new UTF8Encoding(false));

        var content = new FileEditorDocumentLoader().Read(path);

        Assert.False(content.Format.EmitBom);
        Assert.Equal("@title 无 BOM\n", content.Text);
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
