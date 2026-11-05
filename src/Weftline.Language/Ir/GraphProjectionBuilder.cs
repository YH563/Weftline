namespace Weftline.Language.Ir;

/// <summary>
/// 从 StoryGraph + GraphAnalysis 构建剧情图的压缩投影。
/// Builds the compressed graph projection from StoryGraph + GraphAnalysis.
/// </summary>
public static class GraphProjectionBuilder
{
    public static GraphProjection Build(ProjectModel model, StoryGraph graph)
        => Build(model, graph, GraphAnalysisBuilder.Build(model, graph));

    public static GraphProjection Build(
        ProjectModel model,
        StoryGraph graph,
        GraphAnalysis analysis)
    {
        var modulesById = graph.Modules.ToDictionary(
            static module => module.Id,
            StringComparer.Ordinal);

        return new GraphProjection(
            graph.Entry,
            BuildChapters(model, graph, analysis),
            BuildModules(graph, analysis),
            BuildModuleEdges(graph, modulesById),
            analysis.ChapterEdges);
    }

    private static IReadOnlyList<GraphChapter> BuildChapters(
        ProjectModel model,
        StoryGraph graph,
        GraphAnalysis analysis)
    {
        var modulesByChapter = new Dictionary<string, List<IrModule>>(StringComparer.Ordinal);
        var chapterOrder = new List<string>();

        foreach (var module in graph.Modules)
        {
            if (!modulesByChapter.TryGetValue(module.ChapterId, out var modules))
            {
                modules = [];
                modulesByChapter[module.ChapterId] = modules;
                chapterOrder.Add(module.ChapterId);
            }

            modules.Add(module);
        }

        var incoming = new HashSet<string>(StringComparer.Ordinal);
        var outgoing = new HashSet<string>(StringComparer.Ordinal);
        var endEdges = new HashSet<string>(StringComparer.Ordinal);

        foreach (var edge in analysis.ChapterEdges)
        {
            if (string.Equals(edge.ToChapter, graph.End, StringComparison.Ordinal))
            {
                endEdges.Add(edge.FromChapter);
            }
            else
            {
                outgoing.Add(edge.FromChapter);
                incoming.Add(edge.ToChapter);
            }
        }

        var chapters = new List<GraphChapter>();
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chapter in model.Chapters)
        {
            var moduleIds = modulesByChapter.TryGetValue(chapter.Id, out var modules)
                ? modules.Select(static module => module.Id).ToList()
                : [];

            chapters.Add(BuildChapter(
                chapter.Id,
                chapter.Title,
                moduleIds,
                graph.Entry,
                incoming,
                outgoing,
                endEdges));

            covered.Add(chapter.Id);
        }

        foreach (var chapterId in chapterOrder)
        {
            if (covered.Contains(chapterId))
            {
                continue;
            }

            var moduleIds = modulesByChapter[chapterId]
                .Select(static module => module.Id)
                .ToList();

            chapters.Add(BuildChapter(
                chapterId,
                chapterId,
                moduleIds,
                graph.Entry,
                incoming,
                outgoing,
                endEdges));
        }

        return chapters;
    }

    private static GraphChapter BuildChapter(
        string chapterId,
        string title,
        IReadOnlyList<string> moduleIds,
        string? entryModuleId,
        IReadOnlySet<string> incoming,
        IReadOnlySet<string> outgoing,
        IReadOnlySet<string> endEdges)
    {
        var flags = GraphChapterFlags.None;

        if (entryModuleId is not null &&
            moduleIds.Contains(entryModuleId, StringComparer.Ordinal))
        {
            flags |= GraphChapterFlags.Entry;
        }

        if (incoming.Contains(chapterId))
        {
            flags |= GraphChapterFlags.HasIncoming;
        }

        if (outgoing.Contains(chapterId))
        {
            flags |= GraphChapterFlags.HasOutgoing;
        }

        if (endEdges.Contains(chapterId))
        {
            flags |= GraphChapterFlags.HasEndEdge;
        }

        return new GraphChapter(chapterId, title, moduleIds, flags);
    }

    private static IReadOnlyList<GraphModuleNode> BuildModules(
        StoryGraph graph,
        GraphAnalysis analysis)
    {
        return graph.Modules
            .Select(module => new GraphModuleNode(
                module.Id,
                module.ChapterId,
                module.Scene,
                module.Exit?.Branches.Count ?? 0,
                BuildModuleFlags(module, graph, analysis),
                BuildSummary(module),
                module.Source))
            .ToList();
    }

    private static GraphModuleFlags BuildModuleFlags(
        IrModule module,
        StoryGraph graph,
        GraphAnalysis analysis)
    {
        var flags = GraphModuleFlags.None;

        if (string.Equals(module.Id, graph.Entry, StringComparison.Ordinal))
        {
            flags |= GraphModuleFlags.Entry;
        }

        if (module.Exit is { } exit &&
            exit.Branches.Any(branch =>
                string.Equals(branch.Target, graph.End, StringComparison.Ordinal)))
        {
            flags |= GraphModuleFlags.TargetsEnd;
        }

        if (!analysis.Reachable.Contains(module.Id))
        {
            flags |= GraphModuleFlags.Unreachable;
        }

        if (!analysis.CanReachEnd.Contains(module.Id))
        {
            flags |= GraphModuleFlags.CannotReachEnd;
        }

        return flags;
    }

    private static GraphModuleSummary BuildSummary(IrModule module)
    {
        var firstLine = module.Content
            .Select(FirstLine)
            .FirstOrDefault(static line => line is not null);

        var dialogueCount = module.Content.Count(
            static content => string.Equals(content.Kind, "dialogue", StringComparison.Ordinal));

        var choiceCount = module.Exit is { Mode: ExitMode.Choice } exit
            ? exit.Branches.Count
            : 0;

        return new GraphModuleSummary(firstLine, dialogueCount, choiceCount);
    }

    private static string? FirstLine(IrContentBlock content)
    {
        var text = content.Kind switch
        {
            "narration" => SegmentText(content.Segments),
            "dialogue" => SegmentText(content.Segments),
            "figure" => content.Path,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var line = text
            .Split('\n', 2, StringSplitOptions.None)[0]
            .Trim();

        return line.Length == 0 ? null : line;
    }

    private static string SegmentText(IReadOnlyList<IrTextSegment>? segments)
        => segments is null
            ? string.Empty
            : string.Concat(segments.Select(static segment => segment.Kind switch
            {
                "text" => segment.Text,
                "ref" => segment.Name,
                _ => string.Empty,
            }));

    private static IReadOnlyList<GraphModuleEdge> BuildModuleEdges(
        StoryGraph graph,
        IReadOnlyDictionary<string, IrModule> modulesById)
    {
        var edges = new List<GraphModuleEdge>();

        foreach (var module in graph.Modules)
        {
            if (module.Exit is not { } exit)
            {
                continue;
            }

            foreach (var branch in exit.Branches)
            {
                var external = branch.Target != graph.End &&
                    modulesById.TryGetValue(branch.Target, out var targetModule) &&
                    !string.Equals(
                        targetModule.ChapterId,
                        module.ChapterId,
                        StringComparison.Ordinal);

                edges.Add(new GraphModuleEdge(
                    module.Id,
                    branch.Target,
                    exit.Mode,
                    branch.Label,
                    branch.Condition,
                    external,
                    branch.Source));
            }
        }

        return edges;
    }
}
