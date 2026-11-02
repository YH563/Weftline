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
        writer.WriteLine("ScriptFile");

        if (script.Globals is { } globals)
        {
            writer.WriteLine($"  Globals ({globals.Variables.Count})");
            foreach (var variable in globals.Variables)
            {
                writer.WriteLine($"    var {variable.Name} = {ToLower(variable.Value)}");
            }
        }

        foreach (var module in script.Modules)
        {
            writer.WriteLine($"  Module \"{module.Title}\"");

            if (module.Characters.Count > 0)
            {
                var names = string.Join(", ", module.Characters.Select(static c => c.Name));
                writer.WriteLine($"    chars: {names}");
            }

            foreach (var block in module.Content)
            {
                switch (block)
                {
                    case NarrationSyntax narration:
                        writer.WriteLine($"    narration: {Short(narration.Text)}");
                        break;
                    case DialogueSyntax dialogue:
                        writer.WriteLine($"    dialogue {dialogue.Speaker}: {Short(dialogue.Text)}");
                        break;
                    case FigureSyntax figure:
                        writer.WriteLine($"    figure: {figure.Path}");
                        break;
                    case SetSyntax set:
                        writer.WriteLine($"    set {set.Name} = {ToLower(set.Value)}");
                        break;
                }
            }

            if (module.Exit is { } exit)
            {
                var mode = exit.IsChoiceMode ? $"choice ({exit.Character})" : "auto";
                writer.WriteLine($"    exit: {mode}");

                foreach (var branch in exit.Branches)
                {
                    var label = branch.Label is null ? string.Empty : $"[{branch.Label}] ";
                    var condition = branch.Condition is null
                        ? string.Empty
                        : $"when {FormatCondition(branch.Condition)} ";
                    var effects = branch.Effects.Count == 0
                        ? string.Empty
                        : " { " + string.Join("; ", branch.Effects.Select(static e => $"{e.Name} = {ToLower(e.Value)}")) + " }";

                    writer.WriteLine($"      {label}{condition}-> {branch.Target}{effects}");
                }
            }
            else
            {
                writer.WriteLine("    exit: <missing>");
            }
        }
    }

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
