namespace Weftline.Language.Projects;

/// <summary>
/// 一次实时编译使用的不可变项目快照。
///
/// Documents 只包含内存中的打开文件；未包含的文件由加载器从磁盘读取。
/// An immutable project snapshot for one real-time compilation.
///
/// Documents contains only open in-memory files; files not present are read
/// from disk by the loader.
/// </summary>
public sealed record ProjectSnapshot(
    string Folder,
    long Version,
    IReadOnlyDictionary<string, string> Documents)
{
    /// <summary>
    /// 创建一个没有内存文档的磁盘快照。
    /// Creates a disk-only snapshot with no in-memory documents.
    /// </summary>
    public static ProjectSnapshot FromFolder(string folder, long version = 0)
        => new(folder, version, new Dictionary<string, string>(StringComparer.Ordinal));
}
