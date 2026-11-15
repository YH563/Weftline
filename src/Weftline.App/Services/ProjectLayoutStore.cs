using System.Text.Encodings.Web;
using System.Text.Json;
using Weftline.App.Features.Graph.Layout;
using Weftline.App.Models;

namespace Weftline.App.Services;

/// <summary>
/// 把剧情图布局写到项目目录下的 weftline.layout.json。
/// 文件读写集中在这里，ViewModel 不再直接接触磁盘。
/// </summary>
public sealed class ProjectLayoutStore : IProjectLayoutStore
{
    /// <summary>侧车文件名。</summary>
    public const string FileName = "weftline.layout.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public GraphLayoutSnapshot? Load(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var path = GetPath(folder);

        if (!File.Exists(path))
        {
            return null;
        }

        var document = JsonSerializer.Deserialize<ProjectLayoutDocument>(
            File.ReadAllText(path), JsonOptions);

        return document?.ToSnapshot();
    }

    public void Save(string folder, GraphLayoutSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        var document = ProjectLayoutDocument.FromSnapshot(snapshot);
        File.WriteAllText(GetPath(folder), JsonSerializer.Serialize(document, JsonOptions));
    }

    public void Clear(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var path = GetPath(folder);

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string GetPath(string folder)
        => Path.Combine(folder, FileName);
}
