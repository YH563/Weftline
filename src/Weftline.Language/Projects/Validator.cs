using Weftline.Language.Diagnostics;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language.Projects;

/// <summary>
/// 项目级图校验：目标存在、入口、可达性、能否到达 END。
/// Project-level graph validation: targets, entry, reachability, and paths to END.
/// </summary>
public static class Validator
{
    public static IReadOnlyList<ProjectDiagnostic> Validate(
        LoadedProject project,
        ProjectIndex index)
    {
        var diagnostics = new List<ProjectDiagnostic>();
        var modules = index.Modules;

        if (modules.Count == 0)
        {
            diagnostics.Add(new ProjectDiagnostic(
                project.WStoryPath ?? project.Folder,
                new Diagnostic(
                    DiagnosticCode.EmptyProject,
                    DiagnosticSeverity.Error,
                    "项目为空，没有可演示的模块。",
                    new TextSpan(0, 0))));
            return diagnostics;
        }

        var entry = GetEntry(project);
        var syntaxByTitle = BuildSyntaxMap(project, modules);

        ValidateTargets(project, modules, diagnostics);

        if (entry is not null)
        {
            var reachable = ReachableFrom(entry, syntaxByTitle, modules);
            foreach (var module in modules.Values)
            {
                if (!reachable.Contains(module.Title))
                {
                    diagnostics.Add(new ProjectDiagnostic(
                        module.FilePath,
                        new Diagnostic(
                            DiagnosticCode.UnreachableModule,
                            DiagnosticSeverity.Warning,
                            $"模块从入口不可达：{module.Title}",
                            module.TitleSpan)));
                }
            }
        }

        var canReachEnd = CanReachEnd(syntaxByTitle, modules);
        foreach (var module in modules.Values)
        {
            if (!canReachEnd.Contains(module.Title))
            {
                diagnostics.Add(new ProjectDiagnostic(
                    module.FilePath,
                    new Diagnostic(
                        DiagnosticCode.PathCannotReachEnd,
                        DiagnosticSeverity.Warning,
                        $"模块无法到达 END：{module.Title}",
                        module.TitleSpan)));
            }
        }

        return diagnostics;
    }

    private static void ValidateTargets(
        LoadedProject project,
        IReadOnlyDictionary<string, ModuleDefinition> modules,
        List<ProjectDiagnostic> diagnostics)
    {
        foreach (var file in project.WeftFiles)
        {
            foreach (var module in file.Ast.Modules)
            {
                if (module.Exit is not { } exit)
                {
                    continue;
                }

                foreach (var branch in exit.Branches)
                {
                    if (string.Equals(branch.Target, "END", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!modules.ContainsKey(branch.Target))
                    {
                        diagnostics.Add(new ProjectDiagnostic(
                            file.FilePath,
                            new Diagnostic(
                                DiagnosticCode.TargetNotFound,
                                DiagnosticSeverity.Error,
                                $"找不到目标模块：{branch.Target}",
                                branch.TargetSpan)));
                    }
                }
            }
        }
    }

    private static string? GetEntry(LoadedProject project)
    {
        foreach (var file in project.WeftFiles)
        {
            if (file.Ast.Modules.Count > 0)
            {
                return file.Ast.Modules[0].Title;
            }
        }

        return null;
    }

    private static Dictionary<string, ModuleSyntax> BuildSyntaxMap(
        LoadedProject project,
        IReadOnlyDictionary<string, ModuleDefinition> modules)
    {
        var map = new Dictionary<string, ModuleSyntax>(StringComparer.Ordinal);

        foreach (var file in project.WeftFiles)
        {
            foreach (var module in file.Ast.Modules)
            {
                if (modules.TryGetValue(module.Title, out var definition) &&
                    string.Equals(definition.FilePath, file.FilePath, StringComparison.Ordinal) &&
                    definition.TitleSpan == module.TitleSpan)
                {
                    map[module.Title] = module;
                }
            }
        }

        return map;
    }

    private static HashSet<string> ReachableFrom(
        string entry,
        IReadOnlyDictionary<string, ModuleSyntax> syntaxByTitle,
        IReadOnlyDictionary<string, ModuleDefinition> modules)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(entry);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (!syntaxByTitle.TryGetValue(current, out var module) ||
                module.Exit is not { } exit)
            {
                continue;
            }

            foreach (var branch in exit.Branches)
            {
                if (modules.ContainsKey(branch.Target) && !visited.Contains(branch.Target))
                {
                    queue.Enqueue(branch.Target);
                }
            }
        }

        return visited;
    }

    private static HashSet<string> CanReachEnd(
        IReadOnlyDictionary<string, ModuleSyntax> syntaxByTitle,
        IReadOnlyDictionary<string, ModuleDefinition> modules)
    {
        var reverse = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var reaches = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        foreach (var (title, module) in syntaxByTitle)
        {
            if (module.Exit is not { } exit)
            {
                continue;
            }

            foreach (var branch in exit.Branches)
            {
                if (string.Equals(branch.Target, "END", StringComparison.Ordinal))
                {
                    if (reaches.Add(title))
                    {
                        queue.Enqueue(title);
                    }

                    continue;
                }

                if (!modules.ContainsKey(branch.Target))
                {
                    continue;
                }

                if (!reverse.TryGetValue(branch.Target, out var sources))
                {
                    sources = new HashSet<string>(StringComparer.Ordinal);
                    reverse[branch.Target] = sources;
                }

                sources.Add(title);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!reverse.TryGetValue(current, out var sources))
            {
                continue;
            }

            foreach (var source in sources)
            {
                if (reaches.Add(source))
                {
                    queue.Enqueue(source);
                }
            }
        }

        return reaches;
    }
}
