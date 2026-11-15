using Weftline.App.Features.Editor.ViewModels;
using Weftline.App.ViewModels;
using Weftline.Core.Language;

namespace Weftline.App.Services;

/// <summary>
/// 主窗口的注入依赖：由 <see cref="AppComposition"/> 组装。
/// ViewModel 依赖这些抽象而不自己 new 具体实现，测试可整组替换。
/// </summary>
public sealed record MainWindowServices(
    IDialogService Dialogs,
    IProjectAnalysisScheduler Analysis,
    IProjectLayoutStore Layouts,
    IDocumentSaver Documents,
    EditorLanguageService Language,
    ProjectExplorerViewModel Explorer,
    EditorHostViewModel Editor,
    DocumentSnapshotProvider Snapshots);
