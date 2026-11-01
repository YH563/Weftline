using Weftline.Cli;
using Weftline.Language;

if (args.Length != 2 || !string.Equals(args[0], "parse", StringComparison.Ordinal))
{
    Console.WriteLine("Weftline Step 1 — 解析器演示");
    Console.WriteLine("用法: dotnet run --project src/Weftline.Cli -- parse <file.weft>");
    return 1;
}

var path = args[1];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"找不到文件: {path}");
    return 1;
}

var result = WeftlineCompiler.ParseFile(path);

foreach (var diagnostic in result.Diagnostics)
{
    var position = result.Source.GetLinePosition(diagnostic.Span.Start);
    Console.WriteLine(
        $"{path}:{position.Line}:{position.Character}: " +
        $"{diagnostic.Severity.ToString().ToLowerInvariant()} " +
        $"{diagnostic.Code}: {diagnostic.Message}");
}

if (!result.HasErrors)
{
    Console.WriteLine();
    Console.WriteLine("AST:");
    AstPrinter.Print(result.Script, Console.Out);
}

return result.HasErrors ? 1 : 0;
