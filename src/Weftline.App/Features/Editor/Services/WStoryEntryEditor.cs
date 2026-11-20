namespace Weftline.App.Features.Editor.Services;

/// <summary>
/// 对 .wstory 文本做最小的结构编辑：只替换/插入 @entry，不重建其他内容。
/// Minimal structural edit for .wstory: replace or insert @entry only.
/// </summary>
public static class WStoryEntryEditor
{
    /// <summary>
    /// 把入口设置为指定模块：删除旧的 @entry 行，并在 @project 行后插入一条。
    /// Sets the entry to the given module, replacing any existing @entry line.
    /// </summary>
    public static bool TrySetEntry(
        string text,
        string moduleId,
        out string updated,
        out string error)
    {
        updated = text;
        error = string.Empty;

        var entry = moduleId.Trim();

        if (entry.Length == 0)
        {
            error = "入口模块不能为空";
            return false;
        }

        if (entry.Contains('\n') || entry.Contains('\r'))
        {
            error = "入口模块不能包含换行";
            return false;
        }

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();

        var projectIndex = lines.FindIndex(IsProjectDirective);

        if (projectIndex < 0)
        {
            error = ".wstory 缺少 @project";
            return false;
        }

        lines.RemoveAll(IsEntryDirective);
        projectIndex = lines.FindIndex(IsProjectDirective);

        if (projectIndex < 0)
        {
            error = ".wstory 缺少 @project";
            return false;
        }

        lines.Insert(projectIndex + 1, $"    @entry {entry}");
        updated = string.Join(newline, lines);
        return true;
    }

    private static bool IsProjectDirective(string line)
        => IsDirective(line, "@project");

    private static bool IsEntryDirective(string line)
        => IsDirective(line, "@entry");

    private static bool IsDirective(string line, string keyword)
    {
        var trimmed = line.TrimStart();

        if (!trimmed.StartsWith(keyword, StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Length == keyword.Length
            || char.IsWhiteSpace(trimmed[keyword.Length]);
    }
}
