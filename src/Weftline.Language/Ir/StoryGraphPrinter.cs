namespace Weftline.Language.Ir;

/// <summary>
/// 以文本形式打印 StoryGraph 的节点和边，便于快速检查图结构。
/// Prints StoryGraph nodes and edges as text for quick inspection.
/// </summary>
public static class StoryGraphPrinter
{
    public static void Print(StoryGraph graph, TextWriter writer)
    {
        writer.WriteLine(
            $"StoryGraph schema={graph.SchemaVersion} entry={graph.Entry ?? "<none>"} end={graph.End}");

        if (graph.Project is { } project)
        {
            writer.WriteLine($"Project \"{project.Name}\"");
        }

        if (graph.Chapters.Count > 0)
        {
            writer.WriteLine("Chapters");
            foreach (var chapter in graph.Chapters)
            {
                var description = chapter.Description is null ? string.Empty : $" description=\"{chapter.Description}\"";
                writer.WriteLine(
                    $"  \"{chapter.Title}\" modules=[{string.Join(", ", chapter.Modules)}]{description}");
            }
        }

        if (graph.ChapterEdges.Count > 0)
        {
            writer.WriteLine("ChapterEdges");
            foreach (var edge in graph.ChapterEdges)
            {
                writer.WriteLine(
                    $"  \"{edge.From}\" -> \"{edge.To}\" via {string.Join(", ", edge.Via)}");
            }
        }

        if (graph.Globals.Count > 0)
        {
            writer.WriteLine("Globals");
            foreach (var (name, value) in graph.Globals)
            {
                writer.WriteLine($"  {name} = {(value ? "true" : "false")}");
            }
        }

        foreach (var module in graph.Modules)
        {
            var scene = module.Scene is null ? string.Empty : $" scene={module.Scene}";
            var chapter = module.Chapter is null ? string.Empty : $" chapter=\"{module.Chapter}\"";
            writer.WriteLine($"Module \"{module.Id}\"{chapter}{scene}");

            foreach (var content in module.Content)
            {
                writer.WriteLine($"  content: {FormatContent(content)}");
            }

            foreach (var exit in module.Exits)
            {
                var label = exit.Label is null ? string.Empty : $"[{exit.Label}] ";
                var condition = exit.Condition is null ? string.Empty : $"when {FormatCondition(exit.Condition)} ";
                var effects = exit.Effects.Count == 0
                    ? string.Empty
                    : " { " + string.Join("; ", exit.Effects.Select(static effect => $"{effect.Name} = {(effect.Value ? "true" : "false")}")) + " }";
                var external = exit.External ? " (external)" : string.Empty;

                writer.WriteLine($"  edge: {label}{condition}-> {exit.Target}{effects}{external}");
            }
        }
    }

    private static string FormatContent(IrContentBlock content)
        => content.Kind switch
        {
            "narration" => $"[{FormatSegments(content.Segments)}]",
            "dialogue" => $"{content.Speaker}:[{FormatSegments(content.Segments)}]",
            "figure" => $"@fig \"{content.Path}\"",
            _ => content.Kind,
        };

    private static string FormatSegments(IReadOnlyList<IrTextSegment>? segments)
        => segments is null
            ? string.Empty
            : string.Concat(segments.Select(static segment => segment.Kind switch
            {
                "text" => segment.Text,
                "ref" => $"{{{segment.Name}}}",
                _ => string.Empty,
            }));

    private static string FormatCondition(IrBoolExpr expression)
        => expression.Kind switch
        {
            "literal" => expression.Value == true ? "true" : "false",
            "ref" => expression.Name ?? string.Empty,
            "not" => $"not ({FormatCondition(expression.Expr!)})",
            "and" => $"({FormatCondition(expression.Left!)} and {FormatCondition(expression.Right!)})",
            "or" => $"({FormatCondition(expression.Left!)} or {FormatCondition(expression.Right!)})",
            _ => "?",
        };
}
