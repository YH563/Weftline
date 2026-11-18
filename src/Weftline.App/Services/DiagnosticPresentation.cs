using Weftline.App.ViewModels;
using Weftline.Core;
using Weftline.Core.Language;
using Weftline.Native;

namespace Weftline.App.Services;

/// <summary>
/// native 诊断到界面模型的映射：UTF-16 偏移解析为 1 起算的行列号，
/// 并给同一批诊断复用按文件缓存的 <see cref="LineIndex"/>。
/// </summary>
public static class DiagnosticPresentation
{
    /// <summary>
    /// 把 native 诊断映射为界面项。位置优先取 <paramref name="documents"/> 中的文本，
    /// 缺失时再调用 <paramref name="readFromDisk"/>（可能为 null）。
    /// </summary>
    public static IReadOnlyList<DiagnosticItem> CreateItems(
        IReadOnlyList<NativeDiagnostic> diagnostics,
        IReadOnlyDictionary<string, string> documents,
        Func<string, string?>? readFromDisk = null)
    {
        var positions = BuildPositionCache(
            diagnostics.Select(static item => item.FilePath),
            documents,
            readFromDisk);

        var items = new List<DiagnosticItem>(diagnostics.Count);

        foreach (var diagnostic in diagnostics)
        {
            var (line, character) = positions.TryGetValue(diagnostic.FilePath, out var index)
                ? index.Resolve(diagnostic.Start)
                : (1, 1);

            items.Add(new DiagnosticItem(
                diagnostic.FilePath,
                line,
                character,
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Message));
        }

        return items;
    }

    /// <summary>把 native 诊断转成编辑器使用的领域模型。</summary>
    public static WeftDiagnostic ToWeftDiagnostic(NativeDiagnostic diagnostic)
        => new(
            diagnostic.FilePath,
            new TextSpan(diagnostic.Start, diagnostic.Length),
            WeftDiagnosticSeverityMapping.FromNative(diagnostic.Severity),
            diagnostic.Code,
            diagnostic.Message);

    /// <summary>把 UTF-16 偏移解析为 1 起算的行列号；没有文本时回退到 (1,1)。</summary>
    public static (int Line, int Character) ResolvePosition(string? text, int offset)
        => string.IsNullOrEmpty(text) ? (1, 1) : new LineIndex(text).Resolve(offset);

    private static Dictionary<string, LineIndex> BuildPositionCache(
        IEnumerable<string> filePaths,
        IReadOnlyDictionary<string, string> documents,
        Func<string, string?>? readFromDisk)
    {
        var cache = new Dictionary<string, LineIndex>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(path) || cache.ContainsKey(path))
            {
                continue;
            }

            var text = documents.TryGetValue(path, out var snapshot)
                ? snapshot
                : readFromDisk?.Invoke(path);

            if (text is not null)
            {
                cache[path] = new LineIndex(text);
            }
        }

        return cache;
    }

    private sealed class LineIndex
    {
        private readonly int[] _starts;
        private readonly int _length;

        public LineIndex(string text)
        {
            _length = text.Length;

            var starts = new List<int> { 0 };
            for (var index = 0; index < text.Length; index++)
            {
                if (text[index] == '\n')
                {
                    starts.Add(index + 1);
                }
            }

            _starts = starts.ToArray();
        }

        public (int Line, int Character) Resolve(int offset)
        {
            offset = Math.Clamp(offset, 0, _length);

            var index = Array.BinarySearch(_starts, offset);
            if (index < 0)
            {
                index = ~index - 1;
            }

            return (index + 1, offset - _starts[index] + 1);
        }
    }
}
