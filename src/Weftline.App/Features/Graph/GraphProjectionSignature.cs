using System.Text;
using Weftline.Core;

namespace Weftline.App.Features.Graph;

/// <summary>
/// 剧情图投影的结构签名：投影内容不变时跳过 UI 重建。
/// 签名只用于相等比较，不做持久化，字段变化时同步修改即可。
/// </summary>
public static class GraphProjectionSignature
{
    /// <summary>生成投影的结构签名。</summary>
    public static string Build(GraphProjection projection)
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
}
