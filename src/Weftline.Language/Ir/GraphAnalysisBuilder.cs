namespace Weftline.Language.Ir;

/// <summary>
/// 从 StoryGraph 构建分析结果：邻接表、可达性、引用索引和章节聚合边。
/// Builds analysis data from a StoryGraph: adjacency, reachability, reference
/// indexes, and aggregated chapter edges.
/// </summary>
public static class GraphAnalysisBuilder
{
    public static GraphAnalysis Build(ProjectModel model, StoryGraph graph)
    {
        var modulesById = graph.Modules.ToDictionary(
            static module => module.Id,
            StringComparer.Ordinal);

        var adjacency = BuildAdjacency(graph);
        var reverseAdjacency = BuildReverseAdjacency(adjacency);

        return new GraphAnalysis(
            ComputeReachable(graph, modulesById, adjacency),
            ComputeCanReachEnd(graph, reverseAdjacency),
            BuildReferences(model, graph),
            adjacency,
            reverseAdjacency,
            BuildChapterEdges(graph, modulesById));
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildAdjacency(StoryGraph graph)
    {
        var adjacency = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var module in graph.Modules)
        {
            var targets = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (module.Exit is { } exit)
            {
                foreach (var branch in exit.Branches)
                {
                    if (seen.Add(branch.Target))
                    {
                        targets.Add(branch.Target);
                    }
                }
            }

            adjacency[module.Id] = targets;
        }

        return adjacency;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildReverseAdjacency(
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency)
    {
        var reverse = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (from, targets) in adjacency)
        {
            foreach (var target in targets)
            {
                if (!reverse.TryGetValue(target, out var sources))
                {
                    sources = [];
                    reverse[target] = sources;
                }

                if (sources.Count == 0 ||
                    !string.Equals(sources[^1], from, StringComparison.Ordinal))
                {
                    sources.Add(from);
                }
            }
        }

        return reverse.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);
    }

    private static HashSet<string> ComputeReachable(
        StoryGraph graph,
        IReadOnlyDictionary<string, IrModule> modulesById,
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);

        if (graph.Entry is not { } entry || !modulesById.ContainsKey(entry))
        {
            return reachable;
        }

        var queue = new Queue<string>();
        queue.Enqueue(entry);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reachable.Add(current) ||
                !adjacency.TryGetValue(current, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                if (target != graph.End &&
                    modulesById.ContainsKey(target) &&
                    !reachable.Contains(target))
                {
                    queue.Enqueue(target);
                }
            }
        }

        return reachable;
    }

    private static HashSet<string> ComputeCanReachEnd(
        StoryGraph graph,
        IReadOnlyDictionary<string, IReadOnlyList<string>> reverseAdjacency)
    {
        var canReachEnd = new HashSet<string>(StringComparer.Ordinal);

        if (!reverseAdjacency.TryGetValue(graph.End, out var directSources))
        {
            return canReachEnd;
        }

        var queue = new Queue<string>();

        foreach (var source in directSources)
        {
            if (canReachEnd.Add(source))
            {
                queue.Enqueue(source);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (!reverseAdjacency.TryGetValue(current, out var sources))
            {
                continue;
            }

            foreach (var source in sources)
            {
                if (canReachEnd.Add(source))
                {
                    queue.Enqueue(source);
                }
            }
        }

        return canReachEnd;
    }

    private static ReferenceIndex BuildReferences(ProjectModel model, StoryGraph graph)
    {
        var characters = NewIndex();
        var items = NewIndex();
        var scenes = NewIndex();
        var variables = NewIndex();

        var characterNames = model.Symbols.Characters
            .Select(static character => character.Name)
            .ToHashSet(StringComparer.Ordinal);

        var itemNames = model.Symbols.Items
            .Select(static item => item.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var module in graph.Modules)
        {
            AddReference(scenes, module.Scene, module.Id);

            foreach (var content in module.Content)
            {
                if (content.Kind == "dialogue")
                {
                    AddReference(characters, content.Speaker, module.Id);
                    AddSegmentReferences(content.Segments, module.Id, characters, items, characterNames, itemNames);
                }
                else if (content.Kind == "narration")
                {
                    AddSegmentReferences(content.Segments, module.Id, characters, items, characterNames, itemNames);
                }
            }

            if (module.Exit is not { } exit)
            {
                continue;
            }

            AddReference(characters, exit.Character, module.Id);

            foreach (var branch in exit.Branches)
            {
                AddConditionVariables(branch.Condition, name => AddReference(variables, name, module.Id));

                foreach (var effect in branch.Effects)
                {
                    AddReference(variables, effect.Name, module.Id);
                }
            }
        }

        return new ReferenceIndex(
            Freeze(characters),
            Freeze(items),
            Freeze(scenes),
            Freeze(variables));
    }

    private static void AddSegmentReferences(
        IReadOnlyList<IrTextSegment>? segments,
        string moduleId,
        Dictionary<string, List<string>> characters,
        Dictionary<string, List<string>> items,
        IReadOnlySet<string> characterNames,
        IReadOnlySet<string> itemNames)
    {
        if (segments is null)
        {
            return;
        }

        foreach (var segment in segments)
        {
            if (segment.Kind != "ref" || segment.Name is not { } name)
            {
                continue;
            }

            if (characterNames.Contains(name))
            {
                AddReference(characters, name, moduleId);
            }
            else if (itemNames.Contains(name))
            {
                AddReference(items, name, moduleId);
            }
        }
    }

    private static void AddConditionVariables(IrBoolExpr? expression, Action<string> add)
    {
        if (expression is null)
        {
            return;
        }

        switch (expression.Kind)
        {
            case "ref":
                if (expression.Name is { } name)
                {
                    add(name);
                }

                break;

            case "not":
                AddConditionVariables(expression.Expr, add);
                break;

            case "and":
            case "or":
                AddConditionVariables(expression.Left, add);
                AddConditionVariables(expression.Right, add);
                break;
        }
    }

    private static IReadOnlyList<ChapterEdge> BuildChapterEdges(
        StoryGraph graph,
        IReadOnlyDictionary<string, IrModule> modulesById)
    {
        var builders = new List<ChapterEdgeBuilder>();
        var indexes = new Dictionary<(string From, string To), int>();

        foreach (var module in graph.Modules)
        {
            if (module.Exit is not { } exit)
            {
                continue;
            }

            foreach (var branch in exit.Branches)
            {
                var targetChapter = branch.Target == graph.End
                    ? graph.End
                    : modulesById.TryGetValue(branch.Target, out var targetModule)
                        ? targetModule.ChapterId
                        : null;

                if (targetChapter is null ||
                    string.Equals(targetChapter, module.ChapterId, StringComparison.Ordinal))
                {
                    continue;
                }

                var key = (module.ChapterId, targetChapter);

                if (!indexes.TryGetValue(key, out var index))
                {
                    index = builders.Count;
                    indexes[key] = index;
                    builders.Add(new ChapterEdgeBuilder(module.ChapterId, targetChapter));
                }

                builders[index].Details.Add(new ChapterEdgeDetail(
                    module.Id,
                    branch.Target,
                    exit.Mode,
                    branch.Label,
                    branch.Condition,
                    branch.Source));
            }
        }

        return builders
            .Select(static builder => new ChapterEdge(
                builder.FromChapter,
                builder.ToChapter,
                builder.Details))
            .ToList();
    }

    private static Dictionary<string, List<string>> NewIndex()
        => new(StringComparer.Ordinal);

    private static void AddReference(
        Dictionary<string, List<string>> index,
        string? name,
        string moduleId)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (!index.TryGetValue(name, out var modules))
        {
            modules = [];
            index[name] = modules;
        }

        if (modules.Count == 0 ||
            !string.Equals(modules[^1], moduleId, StringComparison.Ordinal))
        {
            modules.Add(moduleId);
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Freeze(
        Dictionary<string, List<string>> index)
        => index.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.Ordinal);

    private sealed class ChapterEdgeBuilder
    {
        public ChapterEdgeBuilder(string fromChapter, string toChapter)
        {
            FromChapter = fromChapter;
            ToChapter = toChapter;
        }

        public string FromChapter { get; }

        public string ToChapter { get; }

        public List<ChapterEdgeDetail> Details { get; } = [];
    }
}
