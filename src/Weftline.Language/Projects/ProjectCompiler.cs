using Weftline.Language.Diagnostics;
using Weftline.Language.Ir;

namespace Weftline.Language.Projects;

/// <summary>
/// 一次项目编译的结果：加载、绑定、校验与 StoryGraph。
/// The result of one project compile: load, bind, validate, and StoryGraph.
/// </summary>
public sealed record ProjectCompilationResult(
    LoadedProject Project,
    ProjectIndex Index,
    IReadOnlyList<ProjectDiagnostic> Diagnostics,
    StoryGraph Graph)
{
    public bool HasErrors => Diagnostics.Any(
        static diagnostic => diagnostic.Diagnostic.Severity == DiagnosticSeverity.Error);
}

/// <summary>
/// 项目级编译入口。
/// Project-level compile entry point.
/// </summary>
public static class ProjectCompiler
{
    public static ProjectCompilationResult Compile(string folder, string? entry = null)
    {
        var project = ProjectLoader.Load(folder);
        var binding = ProjectBinder.Bind(project.WeftFiles, project.WStory, project.WStoryPath);

        var diagnostics = new List<ProjectDiagnostic>();
        diagnostics.AddRange(project.Diagnostics);
        diagnostics.AddRange(binding.Diagnostics);
        diagnostics.AddRange(Validator.Validate(project, binding.Index));

        var graph = StoryGraphBuilder.Build(project, binding.Index, entry);

        return new ProjectCompilationResult(project, binding.Index, diagnostics, graph);
    }
}
