using Weftline.Editor.Models;

namespace Weftline.Editor.Services;

public interface IEditorIconProvider
{
    EditorIcon Resolve(string name, bool isDirectory, bool isExpanded);
}
