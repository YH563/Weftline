namespace Weftline.Editor.Services;

/// <summary>
/// Weftline 编辑器参与语法/语言服务的文件类型。
/// </summary>
public static class WeftDocumentKinds
{
    public static bool IsWeftFile(string? path)
    {
        var extension = Path.GetExtension(path);

        return string.Equals(extension, ".weft", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".wstory", StringComparison.OrdinalIgnoreCase);
    }
}
