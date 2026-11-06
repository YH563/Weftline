using System.Runtime.InteropServices;
using System.Text.Json;

namespace Weftline.Native;

/// <summary>
/// C++ core 的项目句柄封装。
/// 单个实例不是线程安全的；请在同一个逻辑线程/串行调用中使用。
/// </summary>
public sealed class NativeWeftlineClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, string> _documents = new(StringComparer.Ordinal);
    private IntPtr _handle;
    private string? _folder;

    /// <summary>
    /// 创建 native core 项目句柄。
    /// </summary>
    public NativeWeftlineClient()
    {
        _handle = NativeMethods.weft_project_create();
        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("weft_project_create returned null.");
        }
    }

    /// <summary>
    /// native core 的版本号。
    /// </summary>
    public string Version
        => Marshal.PtrToStringUTF8(NativeMethods.weft_version()) ?? string.Empty;

    /// <summary>
    /// 从磁盘读取顶层 .weft/.wstory，并用打开文档的内存文本覆盖。
    /// </summary>
    public void LoadFolder(string folder, IReadOnlyDictionary<string, string> openDocuments)
    {
        var fullFolder = Path.GetFullPath(folder);

        if (!string.Equals(_folder, fullFolder, StringComparison.Ordinal))
        {
            NativeMethods.weft_project_set_folder(_handle, fullFolder);
            _folder = fullFolder;
        }

        var documents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pattern in new[] { "*.weft", "*.wstory" })
        {
            foreach (var path in Directory.EnumerateFiles(
                         fullFolder, pattern, SearchOption.TopDirectoryOnly))
            {
                documents[Path.GetFullPath(path)] = File.ReadAllText(path);
            }
        }

        foreach (var pair in openDocuments)
        {
            documents[Normalize(fullFolder, pair.Key)] = pair.Value;
        }

        foreach (var path in _documents.Keys.Except(documents.Keys).ToList())
        {
            NativeMethods.weft_project_remove_document(_handle, path);
            _documents.Remove(path);
        }

        foreach (var pair in documents)
        {
            NativeMethods.weft_project_set_document(_handle, pair.Key, pair.Value);
            _documents[pair.Key] = pair.Value;
        }
    }

    /// <summary>
    /// 编译当前项目并反序列化 native 响应。
    /// </summary>
    /// <param name="allowPartial">有错误时是否仍构建部分图。</param>
    /// <param name="buildProjection">是否构建图投影。</param>
    public NativeProjectResponse Compile(bool allowPartial, bool buildProjection)
    {
        var pointer = NativeMethods.weft_project_compile(
            _handle, allowPartial ? 1 : 0, buildProjection ? 1 : 0);

        if (pointer == IntPtr.Zero)
        {
            throw new InvalidOperationException("weft_project_compile returned null.");
        }

        try
        {
            var json = Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
            var response = JsonSerializer.Deserialize<NativeProjectResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Native response was empty.");

            if (!string.IsNullOrEmpty(response.Error))
            {
                throw new InvalidOperationException(response.Error);
            }

            return response;
        }
        finally
        {
            NativeMethods.weft_free(pointer);
        }
    }

    /// <summary>
    /// 释放 native 项目句柄。
    /// </summary>
    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.weft_project_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    private static string Normalize(string folder, string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(folder, path));
}
