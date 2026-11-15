using Weftline.App.Features.Editor.Models;

namespace Weftline.App.Features.Editor.Services;

public interface IEditorIconProvider
{
    EditorIcon Resolve(string name, bool isDirectory, bool isExpanded);
}
