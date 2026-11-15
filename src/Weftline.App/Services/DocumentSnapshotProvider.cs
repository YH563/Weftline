using Weftline.App.Features.Editor.Services;
using Weftline.App.Features.Editor.ViewModels;

namespace Weftline.App.Services;

/// <summary>
/// 收集所有打开的 .weft 文档的文本快照。
/// 文档版本未变时复用上一次快照，避免每次编译都重新取全文。
/// </summary>
public sealed class DocumentSnapshotProvider
{
    private readonly EditorHostViewModel _editor;
    private readonly Dictionary<string, SnapshotEntry> _snapshots = new(StringComparer.Ordinal);

    public DocumentSnapshotProvider(EditorHostViewModel editor)
        => _editor = editor;

    /// <summary>文档集合变化或切换项目时清空快照缓存。</summary>
    public void Clear() => _snapshots.Clear();

    /// <summary>生成当前打开文档的路径 → 文本映射。</summary>
    public IReadOnlyDictionary<string, string> GetDocuments()
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var document in _editor.Documents)
        {
            if (string.IsNullOrWhiteSpace(document.FilePath) ||
                !WeftDocumentKinds.IsWeftFile(document.FilePath))
            {
                continue;
            }

            if (_snapshots.TryGetValue(document.FilePath, out var snapshot) &&
                ReferenceEquals(snapshot.Owner, document) &&
                snapshot.Version == document.Version)
            {
                documents[document.FilePath] = snapshot.Text;
                continue;
            }

            var text = document.GetTextSnapshot();
            _snapshots[document.FilePath] = new SnapshotEntry(document, document.Version, text);
            documents[document.FilePath] = text;
        }

        return documents;
    }

    private readonly record struct SnapshotEntry(
        EditorDocumentViewModel Owner,
        long Version,
        string Text);
}
