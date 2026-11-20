using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Features.Editor.Services;
using Weftline.App.Features.Editor.ViewModels;
using Weftline.App.Features.Graph;
using Weftline.App.Features.Graph.Layout;
using Weftline.App.Features.Graph.ViewModels;
using Weftline.App.Features.Player.ViewModels;
using Weftline.App.Models;
using Weftline.App.Services;
using Weftline.Core;
using Weftline.Core.Language;
using Weftline.Core.Runtime;
using Weftline.Native;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var folder = await _dialogs.PickFolderAsync("打开项目文件夹").ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        if (!await ConfirmDiscardChangesAsync("切换项目").ConfigureAwait(true))
        {
            return;
        }

        await OpenProjectCoreAsync(folder).ConfigureAwait(true);
    }

    public async Task LoadProjectForPreviewAsync(string folder, string? file)
    {
        await OpenProjectCoreAsync(folder).ConfigureAwait(true);

        if (!string.IsNullOrWhiteSpace(file))
        {
            Editor.OpenFile(ResolvePreviewFile(folder, file));
        }
    }

    /// <summary>
    /// 把 --file 解析成绝对路径。相对路径按项目目录解析，找不到时回退到当前工作目录。
    /// 必须解析成绝对路径：否则同一个文件会以两个 key 登记两次，
    /// 让 native core 把每个声明都判成重复。
    /// </summary>
    private static string ResolvePreviewFile(string folder, string file)
    {
        if (Path.IsPathRooted(file))
        {
            return Path.GetFullPath(file);
        }

        var inProject = Path.GetFullPath(Path.Combine(folder, file));
        return File.Exists(inProject) || !File.Exists(file)
            ? inProject
            : Path.GetFullPath(file);
    }

    private async Task OpenProjectCoreAsync(string folder)
    {
        await _analysisScheduler.ResetAsync().ConfigureAwait(true);
        Editor.CloseAll();
        _snapshots.Clear();
        Explorer.Load(folder);
        _projectFolder = folder;
        StartWatching(folder);
        _lastGoodProjection = null;
        _lastRuntime = null;
        _lastSymbols = null;
        _lastGraphSignature = null;
        _editorLanguageService.Projection = null;
        _editorLanguageService.Symbols = null;
        Graph.Reset();
        Preview.Reset();
        await CompileNowAsync().ConfigureAwait(true);
        ProjectOpened?.Invoke(this, EventArgs.Empty);
        Status.SetStatus($"已打开：{GetFolderName(folder)}");
    }

    [RelayCommand]
    private void NewProject() => Status.SetStatus("新建项目待实现");

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Editor.SelectedDocument is not { } document)
        {
            Status.SetStatus("没有可保存的文件");
            return;
        }

        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            Status.SetStatus("请先打开或新建文件");
            return;
        }

        try
        {
            await _documentSaver
                .SaveAsync(document.FilePath, document.GetTextSnapshot())
                .ConfigureAwait(true);
            document.IsModified = false;
            await CompileNowAsync().ConfigureAwait(true);
            Status.SetStatus($"已保存：{Path.GetFileName(document.FilePath)}");
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, $"保存失败：{document.FilePath}");
            Status.SetStatus($"保存失败：{exception.Message}");
        }
    }

    [RelayCommand]
    private void SaveLayout() => LayoutSaveRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 把视图导出的布局快照交给布局存储。
    /// Hands the layout snapshot exported by the view to the layout store.
    /// </summary>
    public void SaveLayout(GraphLayoutSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.SetStatus("没有打开项目");
            return;
        }

        try
        {
            _layoutStore.Save(_projectFolder, snapshot);
            Status.SetStatus("已保存布局");
        }
        catch (Exception exception)
        {
            Status.SetStatus($"保存布局失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 读取当前项目的布局侧车；不存在时返回 null。
    /// Loads the current project layout sidecar; returns null when missing.
    /// </summary>
    public GraphLayoutSnapshot? LoadLayout()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return null;
        }

        try
        {
            return _layoutStore.Load(_projectFolder);
        }
        catch (Exception exception)
        {
            Status.SetStatus($"布局加载失败：{exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 删除当前项目的布局侧车，配合「重置布局」。
    /// Deletes the current project layout sidecar, used by Reset Layout.
    /// </summary>
    public void ClearSavedLayout()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return;
        }

        try
        {
            _layoutStore.Clear(_projectFolder);
            Status.SetStatus("布局已重置");
        }
        catch (Exception exception)
        {
            Status.SetStatus($"清除布局失败：{exception.Message}");
        }
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private void Undo() => Status.SetStatus("撤销：请使用 Ctrl+Z");

    [RelayCommand]
    private void Redo() => Status.SetStatus("重做：请使用 Ctrl+Y");

    [RelayCommand]
    private void ShowProblems() => IsProblemsVisible = !IsProblemsVisible;

    [RelayCommand]
    private void ShowOutput() => Status.SetStatus("输出面板待接入");

    [RelayCommand]
    private void ShowProjectInfo() => Status.SetStatus("项目总览待接入");

    [RelayCommand]
    private async Task ValidateAsync()
    {
        await CompileNowAsync().ConfigureAwait(true);
        Status.SetStatus("校验完成");
    }

    [RelayCommand]
    private async Task CompileProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.SetStatus("没有打开项目");
            return;
        }

        await CompileNowAsync().ConfigureAwait(true);
        Status.SetStatus("编译完成");
    }

    [RelayCommand]
    private void About() => Status.SetStatus("Weftline · 剧情 demo / 叙事演示工具");
}
