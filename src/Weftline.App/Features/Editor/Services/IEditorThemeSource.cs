using Weftline.App.Features.Editor.Rendering;

namespace Weftline.App.Features.Editor.Services;

public interface IEditorThemeSource
{
    EditorTheme Current { get; }

    event EventHandler? Changed;
}
