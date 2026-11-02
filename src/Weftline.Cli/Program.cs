using Weftline.Cli;
using Weftline.Language;
using Weftline.Language.Diagnostics;
using Weftline.Language.Generation;
using Weftline.Language.Projects;
using Weftline.Language.Source;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0] switch
{
    "parse" when args.Length == 2 => Parse(args[1]),
    "sync" when args.Length == 2 => Sync(args[1]),
    _ => PrintUsageAndFail(),
};

static int Parse(string path)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"File not found: {path}");
        return 1;
    }

    var result = WeftlineCompiler.CompileFile(path);
    PrintDiagnostics(path, result.Source, result.Diagnostics);

    if (!result.HasErrors)
    {
        Console.WriteLine();
        Console.WriteLine("AST:");
        AstPrinter.Print(result.Script, Console.Out);
    }

    return result.HasErrors ? 1 : 0;
}

static int Sync(string folder)
{
    if (!Directory.Exists(folder))
    {
        Console.Error.WriteLine($"Folder not found: {folder}");
        return 1;
    }

    var project = ProjectLoader.Load(folder);

    foreach (var group in project.Diagnostics.GroupBy(static item => item.FilePath))
    {
        var source = SourceText.From(File.ReadAllText(group.Key), group.Key);

        foreach (var item in group)
        {
            var position = source.GetLinePosition(item.Diagnostic.Span.Start);
            Console.WriteLine(
                $"{item.FilePath}:{position.Line}:{position.Character}: " +
                $"{item.Diagnostic.Severity.ToString().ToLowerInvariant()} " +
                $"{item.Diagnostic.Code}: {item.Diagnostic.Message}");
        }
    }

    if (project.Diagnostics.Any(static item => item.Diagnostic.Severity == DiagnosticSeverity.Error))
    {
        return 1;
    }

    var target = project.WStoryPath ?? Path.Combine(folder, "project.wstory");
    var fallbackName = new DirectoryInfo(folder).Name;
    var text = WStoryGenerator.Generate(project.WeftFiles, project.WStory, fallbackName);
    File.WriteAllText(target, text);

    Console.WriteLine($"Wrote {target}");
    return 0;
}

static void PrintDiagnostics(
    string path,
    SourceText source,
    IReadOnlyList<Diagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
    {
        var position = source.GetLinePosition(diagnostic.Span.Start);
        Console.WriteLine(
            $"{path}:{position.Line}:{position.Character}: " +
            $"{diagnostic.Severity.ToString().ToLowerInvariant()} " +
            $"{diagnostic.Code}: {diagnostic.Message}");
    }
}

static void PrintUsage()
{
    Console.WriteLine("Weftline compiler");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Weftline.Cli -- parse <file.weft|file.wstory>");
    Console.WriteLine("  dotnet run --project src/Weftline.Cli -- sync <folder>");
}

static int PrintUsageAndFail()
{
    PrintUsage();
    return 1;
}
