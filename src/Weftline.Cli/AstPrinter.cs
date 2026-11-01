using Weftline.Language.Syntax;

namespace Weftline.Cli;

/// <summary>
/// 把 AST 打印成缩进树，方便用眼睛检查解析结果。
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
                writer.WriteLine(
                    $"    exit: {exit.RawLines.Count} raw line(s) — parsed in Step 2");
            }
            else
            {
                writer.WriteLine("    exit: <missing>");
            }
        }
    }

    private static string ToLower(bool value) => value ? "true" : "false";

    private static string Short(string text)
    {
        text = text.Replace("\r", " ").Replace("\n", " | ");
        return text.Length <= 60 ? text : text[..57] + "...";
    }
}
