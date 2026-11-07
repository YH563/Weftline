using Weftline.Editor.Rendering;

namespace Weftline.Editor.Services;

public interface IEditorThemeSource
{
    EditorTheme Current { get; }

    event EventHandler? Changed;
}
