using Weftline.Language.Diagnostics;
using Weftline.Language.Ir;

namespace Weftline.Language.Projects;

/// <summary>
/// 一次项目编译的结果：加载、绑定、校验、源级模型与 StoryGraph。
/// The result of one project compile: load, bind, validate, source model, and StoryGraph.
/// </summary>
public sealed record ProjectCompilationResult(
    LoadedProject Project,
    ProjectIndex Index,
    ProjectModel Model,
    IReadOnlyList<ProjectDiagnostic> Diagnostics,
    StoryGraph? Graph,
    GraphAnalysis? Analysis,
    GraphProjection? Projection)
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
    /// <summary>
    /// 从磁盘文件夹做严格编译：有 error 时不产出 StoryGraph。
    /// Strict folder compilation: no StoryGraph is produced when there are errors.
    /// </summary>
    public static ProjectCompilationResult Compile(string folder, string? entry = null)
        => Compile(ProjectSnapshot.FromFolder(folder), null, null, entry);

    /// <summary>
    /// 从内存快照编译。实时预览使用 AllowPartial + BuildProjection。
    /// Compiles from an in-memory snapshot. Real-time preview enables AllowPartial + BuildProjection.
    /// </summary>
    public static ProjectCompilationResult Compile(
        ProjectSnapshot snapshot,
        CompilationCache? cache = null,
        CompilationOptions? options = null,
        string? entry = null)
    {
        options ??= new CompilationOptions();

        var project = ProjectLoader.Load(snapshot, cache);
        var binding = ProjectBinder.Bind(project.WeftFiles, project.WStory, project.WStoryPath);

        var diagnostics = new List<ProjectDiagnostic>();
        diagnostics.AddRange(project.Diagnostics);
        diagnostics.AddRange(binding.Diagnostics);
        diagnostics.AddRange(Validator.Validate(project, binding.Index));

        var model = ProjectModelBuilder.Build(project, binding.Index);

        var hasErrors = diagnostics.Any(
            static diagnostic => diagnostic.Diagnostic.Severity == DiagnosticSeverity.Error);

        var graph = !hasErrors || options.AllowPartial
            ? StoryGraphBuilder.Build(project, model, entry)
            : null;

        var analysis = graph is null
            ? null
            : GraphAnalysisBuilder.Build(model, graph);

        var projection = options.BuildProjection && graph is not null && analysis is not null
            ? GraphProjectionBuilder.Build(model, graph, analysis)
            : null;

        return new ProjectCompilationResult(
            project,
            binding.Index,
            model,
            diagnostics,
            graph,
            analysis,
            projection);
    }
}
