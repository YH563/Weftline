using Weftline.App.Features.Editor.ViewModels;
using Weftline.App.ViewModels;
using Weftline.Core.Language;
using Weftline.Native;

namespace Weftline.App.Services;

/// <summary>
/// 应用组合根：唯一知道具体实现（native client、文件系统、语言服务）的地方。
/// Program/App 只调用这里，测试则直接构造 <see cref="MainWindowServices"/>。
/// </summary>
public static class AppComposition
{
    /// <summary>组装主窗口及其全部依赖。</summary>
    public static MainWindowViewModel CreateMainWindow(IDialogService dialogs)
        => new(CreateServices(dialogs));

    /// <summary>组装主窗口依赖；测试可复用其中的默认实现。</summary>
    public static MainWindowServices CreateServices(IDialogService dialogs)
    {
        var language = new EditorLanguageService(new NativeWeftSyntaxParser());
        var editor = new EditorHostViewModel(
            new FileEditorDocumentLoader(),
            new EditorIconProvider(),
            new EditorThemeSource(),
            language);
        var snapshots = new DocumentSnapshotProvider(editor);

        return new MainWindowServices(
            dialogs,
            new ProjectAnalysisScheduler(
                snapshots.GetDocuments,
                () => new NativeWeftlineClient()),
            new ProjectLayoutStore(),
            new FileDocumentSaver(),
            language,
            new ProjectExplorerViewModel(dialogs),
            editor,
            snapshots);
    }
}
