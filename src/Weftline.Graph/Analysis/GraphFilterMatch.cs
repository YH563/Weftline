using Weftline.Models;

namespace Weftline.Graph.Analysis;

/// <summary>
/// 筛选命中的模块、模块边和章节边。
/// Modules, module edges, and chapter edges matched by a filter.
/// </summary>
public sealed record GraphFilterMatch(
    IReadOnlySet<string> Modules,
    IReadOnlySet<(string From, string To)> ModuleEdges,
    IReadOnlySet<(string FromChapter, string ToChapter)> ChapterEdges)
{
    public static GraphFilterMatch Empty { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<(string From, string To)>(),
        new HashSet<(string FromChapter, string ToChapter)>());
}

/// <summary>
/// 按角色/场景/变量筛选模块：同维度 OR，跨维度 AND。
/// Matches modules by character/scene/variable: OR within a dimension, AND across.
/// </summary>
public static class GraphFilterMatcher
{
    public static GraphFilterMatch Match(
        GraphProjection? projection,
        GraphFilterSelection? filter)
    {
        if (projection is null || filter is null || filter.IsEmpty)
        {
            return GraphFilterMatch.Empty;
        }

        var moduleCharacters = Invert(projection.References.Characters);
        var moduleVariables = Invert(projection.References.Variables);
        var modules = new HashSet<string>(StringComparer.Ordinal);

        foreach (var module in projection.Modules)
        {
            var matchesCharacters = filter.Characters.Count == 0 ||
                (moduleCharacters.TryGetValue(module.ModuleId, out var characters) &&
                 characters.Overlaps(filter.Characters));

            var matchesScenes = filter.Scenes.Count == 0 ||
                module.Scenes.Any(filter.Scenes.Contains);

            var matchesVariables = filter.Variables.Count == 0 ||
                (moduleVariables.TryGetValue(module.ModuleId, out var variables) &&
                 variables.Overlaps(filter.Variables));

            if (matchesCharacters && matchesScenes && matchesVariables)
            {
                modules.Add(module.ModuleId);
            }
        }

        var moduleEdges = new HashSet<(string From, string To)>();

        foreach (var edge in projection.ModuleEdges)
        {
            var sourceVisible = modules.Contains(edge.FromModule);
            var targetVisible = string.Equals(edge.ToModule, "END", StringComparison.Ordinal)
                ? sourceVisible
                : modules.Contains(edge.ToModule);

            if (sourceVisible && targetVisible)
            {
                moduleEdges.Add((edge.FromModule, edge.ToModule));
            }
        }

        var chapterEdges = new HashSet<(string FromChapter, string ToChapter)>();

        foreach (var chapterEdge in projection.ChapterEdges)
        {
            if (chapterEdge.Details.Any(detail =>
                    moduleEdges.Contains((detail.FromModule, detail.ToModule))))
            {
                chapterEdges.Add((chapterEdge.FromChapter, chapterEdge.ToChapter));
            }
        }

        return new GraphFilterMatch(modules, moduleEdges, chapterEdges);
    }

    private static Dictionary<string, HashSet<string>> Invert(
        IReadOnlyDictionary<string, IReadOnlyList<string>> references)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var (name, modules) in references)
        {
            foreach (var moduleId in modules)
            {
                if (!result.TryGetValue(moduleId, out var names))
                {
                    names = new HashSet<string>(StringComparer.Ordinal);
                    result[moduleId] = names;
                }

                names.Add(name);
            }
        }

        return result;
    }
}
