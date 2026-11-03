using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Weftline.Language.Ir;

/// <summary>
/// StoryGraph 的 JSON 序列化。
/// JSON serialization for StoryGraph.
/// </summary>
public static class StoryGraphJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(StoryGraph graph)
        => JsonSerializer.Serialize(graph, Options);
}
