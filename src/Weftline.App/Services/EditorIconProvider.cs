using Weftline.App.Features.Editor.Models;
using Weftline.App.Features.Editor.Services;

namespace Weftline.App.Services;

public sealed class EditorIconProvider : IEditorIconProvider
{
    public EditorIcon Resolve(string name, bool isDirectory, bool isExpanded)
    {
        var icon = FileIconResolver.Resolve(name, isDirectory, isExpanded);
        return new EditorIcon(icon.Geometry, icon.Brush);
    }
}
