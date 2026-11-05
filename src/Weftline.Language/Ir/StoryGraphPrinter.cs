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

        if (graph.Globals.Count > 0)
        {
            writer.WriteLine("Globals");
            foreach (var global in graph.Globals)
            {
                writer.WriteLine($"  {global.Name} = {(global.Initial ? "true" : "false")}");
            }
        }

        foreach (var module in graph.Modules)
        {
            var scene = module.Scene is null ? string.Empty : $" scene={module.Scene}";
            writer.WriteLine($"Module \"{module.Id}\" chapter=\"{module.ChapterId}\"{scene}");

            foreach (var content in module.Content)
            {
                writer.WriteLine($"  content: {FormatContent(content)}");
            }

            if (module.Exit is not { } exit)
            {
                continue;
            }

            var character = exit.Character is null ? string.Empty : $" character={exit.Character}";
            writer.WriteLine($"  exit: {exit.Mode.ToString().ToLowerInvariant()}{character}");

            foreach (var branch in exit.Branches)
            {
                var label = branch.Label is null ? string.Empty : $"[{branch.Label}] ";
                var condition = branch.Condition is null ? string.Empty : $"when {FormatCondition(branch.Condition)} ";
                var effects = branch.Effects.Count == 0
                    ? string.Empty
                    : " { " + string.Join("; ", branch.Effects.Select(static effect => $"{effect.Name} = {(effect.Value ? "true" : "false")}")) + " }";

                writer.WriteLine($"    edge: {label}{condition}-> {branch.Target}{effects}");
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
