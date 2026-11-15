namespace Weftline.App.Services;

/// <summary>
/// 文档落盘端口：ViewModel 只负责决定"要保存什么"，写盘由实现负责。
/// </summary>
public interface IDocumentSaver
{
    Task SaveAsync(string path, string text, CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认实现：直接写文件系统。
/// </summary>
public sealed class FileDocumentSaver : IDocumentSaver
{
    public Task SaveAsync(string path, string text, CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, text, cancellationToken);
}
