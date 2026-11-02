using Weftline.Language.Syntax;

namespace Weftline.Cli;

/// <summary>
/// 以缩进树的形式打印 AST，便于快速检查。
/// Prints the AST as an indented tree for quick inspection.
/// </summary>
internal static class AstPrinter
{
    public static void Print(ScriptFileSyntax script, TextWriter writer)
    {
        switch (script)
        {
            case WStoryFileSyntax wstory:
                PrintWStory(wstory, writer);
                break;

            case WeftFileSyntax weft:
                PrintWeft(weft, writer);
                break;

            default:
                writer.WriteLine("ScriptFile");
                break;
        }
    }

    private static void PrintWStory(WStoryFileSyntax wstory, TextWriter writer)
    {
        writer.WriteLine($"WStory \"{wstory.ProjectName}\"");

        if (wstory.Summary is { } summary)
        {
            writer.WriteLine($"    summary: [{Short(summary.Text)}]");
        }

        foreach (var doc in wstory.Docs)
        {
            writer.WriteLine($"    doc \"{doc.Title}\": [{Short(doc.Body.Text)}]");
        }

        if (wstory.Characters.Count > 0)
        {
            writer.WriteLine("    chars");
            foreach (var character in wstory.Characters)
            {
                writer.WriteLine($"        char {character.Name}{Description(character.Description)}");
            }
        }

        if (wstory.Items.Count > 0)
        {
            writer.WriteLine("    items");
            foreach (var item in wstory.Items)
            {
                writer.WriteLine($"        item {item.Name}{Description(item.Description)}");
            }
        }

        if (wstory.Scenes.Count > 0)
        {
            writer.WriteLine("    scenes");
            foreach (var scene in wstory.Scenes)
            {
                writer.WriteLine($"        scene {scene.Name}{Description(scene.Description)}");
            }
        }

        if (wstory.Variables.Count > 0)
        {
            writer.WriteLine("    vars");
            foreach (var variable in wstory.Variables)
            {
                writer.WriteLine($"        var {variable.Name} = {ToLower(variable.Value)}{Description(variable.Description)}");
            }
        }
    }

    private static void PrintWeft(WeftFileSyntax weft, TextWriter writer)
    {
        writer.WriteLine("WeftFile");

        if (weft.Variables.Count > 0)
        {
            writer.WriteLine("    vars");
            foreach (var variable in weft.Variables)
            {
                writer.WriteLine($"        var {variable.Name} = {ToLower(variable.Value)}");
            }
        }

        if (weft.Characters.Count > 0)
        {
            writer.WriteLine("    chars");
            foreach (var character in weft.Characters)
            {
                writer.WriteLine($"        char {character.Name}");
            }
        }

        if (weft.Items.Count > 0)
        {
            writer.WriteLine("    items");
            foreach (var item in weft.Items)
            {
                writer.WriteLine($"        item {item.Name}");
            }
        }

        if (weft.Scenes.Count > 0)
        {
            writer.WriteLine("    scenes");
            foreach (var scene in weft.Scenes)
            {
                writer.WriteLine($"        scene {scene.Name}");
            }
        }

        foreach (var module in weft.Modules)
        {
            var scene = module.Scene is null ? string.Empty : $" scene={module.Scene}";
            writer.WriteLine($"    Module \"{module.Title}\"{scene}");

            foreach (var block in module.Content)
            {
                switch (block)
                {
                    case NarrationSyntax narration:
                        writer.WriteLine($"        [{Short(FormatSegments(narration.Segments))}]");
                        break;
                    case DialogueSyntax dialogue:
                        writer.WriteLine($"        {dialogue.Speaker}:[{Short(FormatSegments(dialogue.Segments))}]");
                        break;
                    case FigureSyntax figure:
                        writer.WriteLine($"        @fig \"{figure.Path}\"");
                        break;
                }
            }

            if (module.Exit is { } exit)
            {
                var mode = exit.IsChoiceMode ? $"choice ({exit.Character})" : "auto";
                writer.WriteLine($"        @next {mode}");

                foreach (var branch in exit.Branches)
                {
                    var label = branch.Label is null ? string.Empty : $"[{branch.Label}] ";
                    var condition = branch.Condition is null
                        ? string.Empty
                        : $"when {FormatCondition(branch.Condition)} ";
                    var effects = branch.Effects.Count == 0
                        ? string.Empty
                        : " { " + string.Join("; ", branch.Effects.Select(e => $"{e.Name} = {ToLower(e.Value)}")) + " }";

                    writer.WriteLine($"            {label}{condition}-> {branch.Target}{effects}");
                }
            }
            else
            {
                writer.WriteLine("        @next <missing>");
            }
        }
    }

    private static string Description(TextBlockSyntax? description)
        => description is null ? string.Empty : $"  [{Short(description.Text)}]";

    private static string FormatSegments(IReadOnlyList<TextSegmentSyntax> segments)
        => string.Concat(segments.Select(
            static segment => segment switch
            {
                TextLiteralSyntax literal => literal.Text,
                TextReferenceSyntax reference => $"{{{reference.Name}}}",
                _ => string.Empty,
            }));

    private static string ToLower(bool value) => value ? "true" : "false";

    private static string FormatCondition(BoolExprSyntax expression)
        => expression switch
        {
            BoolLiteralSyntax literal => ToLower(literal.Value),
            BoolRefSyntax reference => reference.Name,
            NotExprSyntax not => $"not ({FormatCondition(not.Operand)})",
            AndExprSyntax and => $"({FormatCondition(and.Left)} and {FormatCondition(and.Right)})",
            OrExprSyntax or => $"({FormatCondition(or.Left)} or {FormatCondition(or.Right)})",
            _ => "?",
        };

    private static string Short(string text)
    {
        text = text.Replace("\r", " ").Replace("\n", " | ");
        return text.Length <= 60 ? text : text[..57] + "...";
    }
}
