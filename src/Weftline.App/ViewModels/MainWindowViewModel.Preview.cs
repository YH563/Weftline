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
    private Task PreviewAsync() => OpenPreviewAsync(startAtCurrentModule: true);

    [RelayCommand]
    private Task PreviewFromStartAsync() => OpenPreviewAsync(startAtCurrentModule: false);

    private async Task OpenPreviewAsync(bool startAtCurrentModule)
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.SetStatus("没有打开项目");
            return;
        }

        Status.SetStatus("正在准备预览…");

        try
        {
            await CompileNowAsync().ConfigureAwait(true);
            var projection = await _analysisScheduler
                .CompileRuntimeAsync()
                .ConfigureAwait(true);

            var startModule = startAtCurrentModule
                ? ResolveCurrentModuleId()
                : projection.Entry;

            var viewModel = new PreviewViewModel(
                projection,
                _projectFolder,
                startModule);

            PreviewRequested?.Invoke(this, new PreviewRequestedEventArgs(viewModel));
            Status.SetStatus("预览已打开");
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, "预览失败");
            Status.SetStatus($"预览失败：{exception.Message}");
            await _dialogs
                .ShowMessageAsync("无法预览", exception.Message)
                .ConfigureAwait(true);
        }
    }

    /// <summary>
    /// 取光标所在模块，供“从当前模块预览”使用；.wstory 或模块之前返回 null。
    /// </summary>
    private string? ResolveCurrentModuleId()
    {
        if (Editor.SelectedDocument is not { } document)
        {
            return null;
        }

        if (!document.FilePath.EndsWith(".weft", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var textDocument = document.Document;

        if (textDocument.TextLength == 0)
        {
            return null;
        }

        var limit = Math.Clamp(document.CaretLine, 1, textDocument.LineCount);
        string? module = null;

        for (var lineNumber = 1; lineNumber <= limit; lineNumber++)
        {
            var line = textDocument
                .GetText(textDocument.GetLineByNumber(lineNumber))
                .Trim();

            if (line.StartsWith("@module ", StringComparison.Ordinal))
            {
                module = line["@module ".Length..].Trim();
            }
        }

        return string.IsNullOrWhiteSpace(module) ? null : module;
    }

    [RelayCommand]
    private void TogglePreviewPanel() => IsPreviewVisible = !IsPreviewVisible;

    partial void OnIsGraphVisibleChanged(bool value)
    {
        if (!value && !IsPreviewVisible)
        {
            IsPreviewVisible = true;
        }
    }

    partial void OnIsPreviewVisibleChanged(bool value)
    {
        if (!value && !IsGraphVisible)
        {
            IsGraphVisible = true;
        }

        if (value)
        {
            _ = EnsurePreviewAsync();
        }
        else
        {
            UpdatePreviewFile();
        }
    }

    public async Task EnsurePreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            return;
        }

        if (_lastRuntime is not null)
        {
            Preview.UpdateProject(
                _projectFolder,
                _lastRuntime,
                _lastGoodProjection,
                isStale: false);
            UpdatePreviewFile();
        }

        await CompileNowAsync(includeRuntime: true).ConfigureAwait(true);
    }

    private void UpdatePreviewFile()
        => Preview.SetFile(Editor.SelectedDocument?.FilePath);
}
