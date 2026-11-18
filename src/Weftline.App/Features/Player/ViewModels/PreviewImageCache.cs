using Avalonia.Media.Imaging;

namespace Weftline.App.Features.Player.ViewModels;

/// <summary>
/// 预览图片缓存：按脚本里的相对路径缓存 Bitmap，切换项目或释放时统一清理。
/// 预览窗口与右侧预览面板共用这一份实现，避免两处各写一套加载/清理逻辑。
/// </summary>
internal sealed class PreviewImageCache : IDisposable
{
    private readonly Dictionary<string, Bitmap?> _images = new(StringComparer.Ordinal);
    private string _projectFolder = string.Empty;

    /// <summary>设置项目根目录；目录变化时清空缓存。</summary>
    public void SetProjectFolder(string folder)
    {
        var root = string.IsNullOrWhiteSpace(folder)
            ? string.Empty
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

        if (string.Equals(_projectFolder, root, StringComparison.Ordinal))
        {
            return;
        }

        _projectFolder = root;
        Clear();
    }

    /// <summary>读取图片；路径为空、越出项目目录或文件不可读时返回 null。</summary>
    public Bitmap? Get(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (_images.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Bitmap? image = null;

        try
        {
            if (_projectFolder.Length > 0)
            {
                var fullPath = Path.GetFullPath(Path.Combine(_projectFolder, path));

                if (fullPath.StartsWith(
                        _projectFolder + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal) &&
                    File.Exists(fullPath))
                {
                    using var stream = File.OpenRead(fullPath);
                    image = new Bitmap(stream);
                }
            }
        }
        catch (Exception)
        {
            image = null;
        }

        _images[path] = image;
        return image;
    }

    /// <summary>释放并清空所有缓存的图片。</summary>
    public void Clear()
    {
        foreach (var image in _images.Values)
        {
            image?.Dispose();
        }

        _images.Clear();
    }

    public void Dispose() => Clear();
}
