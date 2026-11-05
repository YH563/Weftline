using System.Text;
using Weftline.Language.Syntax;

namespace Weftline.Language.Generation;

/// <summary>
/// 从 `.weft` 顶部声明生成 `.wstory`。
///
/// 声明从 `.weft` 反射；项目信息、概要和文档保留现有 `.wstory` 的内容；
/// 描述按名字保留，新条目的描述为空。
///
/// Generates `.wstory` from `.weft` top-level declarations.
///
/// Declarations are reflected from `.weft`; project info, summary, and docs
/// come from the existing `.wstory`; descriptions are preserved by name, and
/// new entries get empty descriptions.
/// </summary>
public static class WStoryGenerator
{
    public static string Generate(
        IReadOnlyList<WeftFileSyntax> weftFiles,
        WStoryFileSyntax? existing,
        string fallbackProjectName)
    {
        var chapters = new List<string>();
        var variables = new List<(string Name, bool Value)>();
        var characters = new List<string>();
        var items = new List<string>();
        var scenes = new List<string>();

        var seenChapters = new HashSet<string>(StringComparer.Ordinal);
        var seenVariables = new HashSet<string>(StringComparer.Ordinal);
        var seenCharacters = new HashSet<string>(StringComparer.Ordinal);
        var seenItems = new HashSet<string>(StringComparer.Ordinal);
        var seenScenes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var weft in weftFiles)
        {
            if (seenChapters.Add(weft.Title))
            {
                chapters.Add(weft.Title);
            }

            foreach (var variable in weft.Variables)
            {
                if (seenVariables.Add(variable.Name))
                {
                    variables.Add((variable.Name, variable.Value));
                }
            }

            foreach (var character in weft.Characters)
            {
                if (seenCharacters.Add(character.Name))
                {
                    characters.Add(character.Name);
                }
            }

            foreach (var item in weft.Items)
            {
                if (seenItems.Add(item.Name))
                {
                    items.Add(item.Name);
                }
            }

            foreach (var scene in weft.Scenes)
            {
                if (seenScenes.Add(scene.Name))
                {
                    scenes.Add(scene.Name);
                }
            }
        }

        var chapterDescriptions = ToDescriptionMap(
            existing?.Chapters.Select(static value => (value.Title, value.Description)));
        var characterDescriptions = ToDescriptionMap(
            existing?.Characters.Select(static value => (value.Name, value.Description)));
        var itemDescriptions = ToDescriptionMap(
            existing?.Items.Select(static value => (value.Name, value.Description)));
        var sceneDescriptions = ToDescriptionMap(
            existing?.Scenes.Select(static value => (value.Name, value.Description)));
        var variableDescriptions = ToDescriptionMap(
            existing?.Variables.Select(static value => (value.Name, value.Description)));

        var projectName = string.IsNullOrWhiteSpace(existing?.ProjectName)
            ? fallbackProjectName
            : existing!.ProjectName;

        var builder = new StringBuilder();
        builder.Append("@project ").AppendLine(projectName);

        if (existing?.Summary is { } summary)
        {
            builder.Append("    @summary ").AppendLine(FormatDescription(summary.Text));
        }

        WriteGroup(builder, "chapters", "chapter", chapters, chapterDescriptions);

        // @docs 是手写的世界设定资料，不是剧情正文；生成时原样保留。
        // @docs are hand-written world-setting notes, not story text; preserve them as-is.
        if (existing is { Docs.Count: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("    @docs");
            foreach (var doc in existing.Docs)
            {
                builder
                    .Append("        @doc ")
                    .Append(doc.Title)
                    .Append(' ')
                    .AppendLine(FormatDescription(doc.Body.Text));
            }
        }

        WriteGroup(builder, "chars", "char", characters, characterDescriptions);
        WriteGroup(builder, "items", "item", items, itemDescriptions);
        WriteGroup(builder, "scenes", "scene", scenes, sceneDescriptions);

        if (variables.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("    @vars");
            foreach (var variable in variables)
            {
                builder
                    .Append("        @var ")
                    .Append(variable.Name)
                    .Append(' ')
                    .Append(variable.Value ? "true" : "false")
                    .Append(' ')
                    .AppendLine(FormatDescription(GetDescription(variableDescriptions, variable.Name)));
            }
        }

        return builder.ToString();
    }

    private static void WriteGroup(
        StringBuilder builder,
        string group,
        string keyword,
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, string?> descriptions)
    {
        if (names.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.Append("    @").AppendLine(group);

        foreach (var name in names)
        {
            builder
                .Append("        @")
                .Append(keyword)
                .Append(' ')
                .Append(name)
                .Append(' ')
                .AppendLine(FormatDescription(GetDescription(descriptions, name)));
        }
    }

    private static Dictionary<string, string?> ToDescriptionMap(
        IEnumerable<(string Name, TextBlockSyntax? Description)>? entries)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (entries is null)
        {
            return map;
        }

        foreach (var (name, description) in entries)
        {
            map[name] = description?.Text;
        }

        return map;
    }

    private static string? GetDescription(
        IReadOnlyDictionary<string, string?> descriptions,
        string name)
        => descriptions.TryGetValue(name, out var description) ? description : null;

    private static string FormatDescription(string? text)
        => string.IsNullOrEmpty(text) ? "[]" : $"[{text}]";
}
