using Weftline.Cli;
using Weftline.Language;
using Weftline.Language.Diagnostics;
using Weftline.Language.Generation;
using Weftline.Language.Ir;
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
    "validate" when args.Length == 2 => Validate(args[1]),
    "ir" when args.Length is 2 or 3 => Ir(args[1], args.Length == 3 ? args[2] : null),
    "graph" when args.Length is 2 or 3 => Graph(args[1], args.Length == 3 ? args[2] : null),
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

static int Validate(string folder)
{
    if (!Directory.Exists(folder))
    {
        Console.Error.WriteLine($"Folder not found: {folder}");
        return 1;
    }

    var result = ProjectCompiler.Compile(folder);
    PrintProjectDiagnostics(result.Diagnostics, Console.Out);

    Console.WriteLine(
        $"Validated {result.Index.Modules.Count} modules, " +
        $"{result.Diagnostics.Count} diagnostics.");

    return result.HasErrors ? 1 : 0;
}

static int Ir(string folder, string? outputPath)
{
    if (!Directory.Exists(folder))
    {
        Console.Error.WriteLine($"Folder not found: {folder}");
        return 1;
    }

    var result = ProjectCompiler.Compile(folder);
    PrintProjectDiagnostics(result.Diagnostics, Console.Error);

    if (result.Graph is not { } graph)
    {
        Console.Error.WriteLine("IR not generated because the project has errors.");
        return 1;
    }

    var json = StoryGraphJson.Serialize(graph);

    if (outputPath is not null)
    {
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Wrote {outputPath}");
    }
    else
    {
        Console.WriteLine(json);
    }

    return result.HasErrors ? 1 : 0;
}

static int Graph(string folder, string? outputPath)
{
    if (!Directory.Exists(folder))
    {
        Console.Error.WriteLine($"Folder not found: {folder}");
        return 1;
    }

    var result = ProjectCompiler.Compile(folder);
    PrintProjectDiagnostics(result.Diagnostics, Console.Error);

    if (result.Graph is not { } graph)
    {
        Console.Error.WriteLine("Graph not generated because the project has errors.");
        return 1;
    }

    StreamWriter? fileWriter = null;
    var writer = Console.Out;

    if (outputPath is not null)
    {
        fileWriter = new StreamWriter(outputPath);
        writer = fileWriter;
    }

    try
    {
        StoryGraphPrinter.Print(graph, writer);
    }
    finally
    {
        fileWriter?.Dispose();
    }

    if (outputPath is not null)
    {
        Console.WriteLine($"Wrote {outputPath}");
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
    PrintProjectDiagnostics(project.Diagnostics, Console.Out);

    if (project.Diagnostics.Any(static item => item.Diagnostic.Severity == DiagnosticSeverity.Error))
    {
        return 1;
    }

    var target = project.WStoryPath ?? Path.Combine(folder, "project.wstory");
    var fallbackName = new DirectoryInfo(folder).Name;
    var text = WStoryGenerator.Generate(
        project.WeftFiles.Select(static file => file.Ast).ToList(),
        project.WStory,
        fallbackName);
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

static void PrintProjectDiagnostics(
    IReadOnlyList<ProjectDiagnostic> diagnostics,
    TextWriter writer)
{
    var sources = new Dictionary<string, SourceText>(StringComparer.Ordinal);

    foreach (var item in diagnostics)
    {
        var position = GetPosition(item.FilePath, item.Diagnostic.Span.Start, sources);
        writer.WriteLine(
            $"{item.FilePath}:{position.Line}:{position.Character}: " +
            $"{item.Diagnostic.Severity.ToString().ToLowerInvariant()} " +
            $"{item.Diagnostic.Code}: {item.Diagnostic.Message}");

        if (item.Related is { } related)
        {
            var relatedPosition = GetPosition(related.FilePath, related.Span.Start, sources);
            writer.WriteLine(
                $"    related: {related.FilePath}:{relatedPosition.Line}:{relatedPosition.Character}");
        }
    }
}

static LinePosition GetPosition(
    string path,
    int offset,
    Dictionary<string, SourceText> sources)
{
    if (!sources.TryGetValue(path, out var source))
    {
        source = File.Exists(path)
            ? SourceText.From(File.ReadAllText(path), path)
            : SourceText.From(string.Empty, path);
        sources[path] = source;
    }

    return source.GetLinePosition(offset);
}

static void PrintUsage()
{
    Console.WriteLine("Weftline compiler");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Weftline.Cli -- parse <file.weft|file.wstory>");
    Console.WriteLine("  dotnet run --project src/Weftline.Cli -- validate <folder>");
    Console.WriteLine("  dotnet run --project src/Weftline.Cli -- ir <folder> [out.json]");
    Console.WriteLine("  dotnet run --project src/Weftline.Cli -- graph <folder> [out.txt]");
    Console.WriteLine("  dotnet run --project src/Weftline.Cli -- sync <folder>");
}

static int PrintUsageAndFail()
{
    PrintUsage();
    return 1;
}
