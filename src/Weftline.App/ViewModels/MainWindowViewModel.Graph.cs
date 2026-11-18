using System.Text;
using Weftline.Core;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel
{
    private void UpdateGraphProjection(GraphProjection? projection)
    {
        var signature = projection is null ? null : BuildGraphSignature(projection);

        if (string.Equals(signature, _lastGraphSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastGraphSignature = signature;
        Graph.Projection = projection;
    }

    private static string BuildGraphSignature(GraphProjection projection)
    {
        var builder = new StringBuilder();
        builder.Append(projection.EntryModuleId).Append('|');

        foreach (var chapter in projection.Chapters)
        {
            builder.Append(chapter.ChapterId).Append('~')
                .Append(chapter.Title).Append('~')
                .Append((int)chapter.Flags).Append(':');

            foreach (var moduleId in chapter.ModuleIds)
            {
                builder.Append(moduleId).Append(',');
            }

            builder.Append('|');
        }

        foreach (var module in projection.Modules)
        {
            builder.Append(module.ModuleId).Append('~')
                .Append((int)module.Flags).Append('~')
                .Append(string.Join(',', module.Scenes)).Append('~')
                .Append(module.Summary.FirstLine).Append('~')
                .Append(module.Summary.DialogueCount).Append('~')
                .Append(module.Summary.ChoiceCount).Append('~');
            AppendSource(builder, module.Source);
            builder.Append('|');
        }

        foreach (var edge in projection.ModuleEdges)
        {
            builder.Append(edge.FromModule).Append('>').Append(edge.ToModule).Append('~')
                .Append((int)edge.Mode).Append('~')
                .Append(edge.Label).Append('~')
                .Append(edge.Condition).Append('~');
            AppendSource(builder, edge.Source);
            builder.Append('|');
        }

        foreach (var edge in projection.ChapterEdges)
        {
            builder.Append(edge.FromChapter).Append('>').Append(edge.ToChapter).Append(':');

            foreach (var detail in edge.Details)
            {
                builder.Append(detail.FromModule).Append('>').Append(detail.ToModule).Append('~')
                    .Append((int)detail.Mode).Append('~')
                    .Append(detail.Label).Append('~')
                    .Append(detail.Condition).Append('~');
                AppendSource(builder, detail.Source);
                builder.Append(',');
            }

            builder.Append('|');
        }

        AppendReferenceMap(builder, 'c', projection.References.Characters);
        AppendReferenceMap(builder, 'i', projection.References.Items);
        AppendReferenceMap(builder, 's', projection.References.Scenes);
        AppendReferenceMap(builder, 'v', projection.References.Variables);

        return builder.ToString();
    }

    private static void AppendReferenceMap(
        StringBuilder builder,
        char kind,
        IReadOnlyDictionary<string, IReadOnlyList<string>> references)
    {
        builder.Append(kind).Append(':');

        foreach (var (name, modules) in references.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            builder.Append(name).Append('=');

            foreach (var moduleId in modules)
            {
                builder.Append(moduleId).Append(',');
            }

            builder.Append(';');
        }

        builder.Append('|');
    }

    private static void AppendSource(StringBuilder builder, SourceLocation source)
        => builder.Append(source.FilePath).Append('@')
            .Append(source.Span.Start).Append(':')
            .Append(source.Span.Length);

    /// <summary>
    /// 打开诊断对应的文件并定位到源码位置。
    /// </summary>
    public void NavigateToDiagnostic(DiagnosticItem diagnostic)
        => Editor.NavigateTo(diagnostic.FilePath, diagnostic.Line, diagnostic.Character);

    /// <summary>
    /// 双击模块：打开所属 .weft 并定位到 @module。
    /// Double-clicking a module opens its .weft file at the module declaration.
    /// </summary>
    public void ActivateModule(GraphModuleNode module)
        => NavigateToSource(module.Source, $"模块 {module.ModuleId}");

    /// <summary>
    /// 双击模块边：定位到对应分支行。
    /// Double-clicking a module edge navigates to the branch line.
    /// </summary>
    public void ActivateModuleEdge(GraphModuleEdge edge)
        => NavigateToSource(edge.Source, $"{edge.FromModule} → {edge.ToModule}");

    /// <summary>
    /// 双击章节边：展示底层模块对明细。
    /// Double-clicking a chapter edge shows its underlying module branches.
    /// </summary>
    public void ActivateChapterEdge(ChapterEdge edge)
    {
        Graph.ShowChapterEdgeDetails(
            $"{edge.FromChapter} → {edge.ToChapter}",
            $"{edge.Count} 条模块分支",
            edge.Details);
        Status.SetStatus($"章节边：{edge.FromChapter} → {edge.ToChapter}");
    }

    /// <summary>
    /// 悬停模块时更新摘要；离开模块时隐藏。
    /// Updates the hover summary for a module; hides it when leaving modules.
    /// </summary>
    public void UpdateModuleHover(GraphModuleNode? module)
    {
        if (module is null)
        {
            Graph.ClearHover();
            return;
        }

        Graph.ShowHover(
            module.ModuleId,
            null,
            $"对话 {module.Summary.DialogueCount} · 选项 {module.Summary.ChoiceCount}",
            null);
    }

    private void NavigateToSource(SourceLocation location, string label)
    {
        if (string.IsNullOrWhiteSpace(location.FilePath))
        {
            return;
        }

        var (line, character) = (1, 1);
        var text = GetDocumentText(location.FilePath);

        if (text is not null)
        {
            (line, character) = new LineIndex(text).Resolve(location.Span.Start);
        }

        Editor.NavigateTo(location.FilePath, line, character);
        Status.SetStatus($"已定位：{label}");
    }
}
