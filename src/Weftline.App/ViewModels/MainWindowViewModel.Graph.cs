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
    /// </summary>
    public void NavigateToDiagnostic(DiagnosticItem diagnostic)
        => Editor.NavigateTo(diagnostic.FilePath, diagnostic.Line, diagnostic.Character);

    /// <summary>
    /// 双击模块：打开所属 .weft 并定位到 @module。
    /// Double-clicking a module opens its .weft file at the module declaration.
    /// </summary>
    public void ActivateModule(GraphModuleNode module)
        => NavigateToSource(module.Source, $"模块 {module.ModuleId}");

    /// <summary>
    /// 双击模块边：定位到对应分支行。
    /// Double-clicking a module edge navigates to the branch line.
    /// </summary>
    public void ActivateModuleEdge(GraphModuleEdge edge)
        => NavigateToSource(edge.Source, $"{edge.FromModule} → {edge.ToModule}");

    /// <summary>
    /// 双击章节边：展示底层模块对明细。
    /// Double-clicking a chapter edge shows its underlying module branches.
    /// </summary>
    public void ActivateChapterEdge(ChapterEdge edge)
    {
        Graph.ShowChapterEdgeDetails(
            $"{edge.FromChapter} → {edge.ToChapter}",
            $"{edge.Count} 条模块分支",
            edge.Details);
        Status.SetStatus($"章节边：{edge.FromChapter} → {edge.ToChapter}");
    }

    /// <summary>
    /// 悬停模块时更新摘要；离开模块时隐藏。
    /// Updates the hover summary for a module; hides it when leaving modules.
    /// </summary>
    public void UpdateModuleHover(GraphModuleNode? module)
    {
        if (module is null)
        {
            Graph.ClearHover();
            return;
        }

        Graph.ShowHover(
            module.ModuleId,
            null,
            $"对话 {module.Summary.DialogueCount} · 选项 {module.Summary.ChoiceCount}",
            null);
    }

    /// <summary>
    /// 把模块设为项目入口：写入 .wstory 并立即保存。
    /// Sets the module as the project entry by editing and saving .wstory immediately.
    /// </summary>
    public async Task SetEntryAsync(GraphModuleNode module)
    {
        if (string.IsNullOrWhiteSpace(_projectFolder) ||
            string.IsNullOrWhiteSpace(module.ModuleId))
        {
            Status.SetStatus("没有可设置入口的项目");
            return;
        }

        try
        {
            var wstoryPath = await EnsureWStoryFileAsync().ConfigureAwait(true);

            if (wstoryPath is null)
            {
                return;
            }

            if (!Editor.OpenFile(wstoryPath) ||
                Editor.SelectedDocument is not { } document)
            {
                Status.SetStatus("无法打开 .wstory");
                return;
            }

            var current = document.GetTextSnapshot();

            if (!WStoryEntryEditor.TrySetEntry(
                    current, module.ModuleId, out var updated, out var error))
            {
                Status.SetStatus($"无法设置入口：{error}");
                return;
            }

            if (string.Equals(current, updated, StringComparison.Ordinal))
            {
                Status.SetStatus($"已是入口：{module.ModuleId}");
                return;
            }

            document.UpdateText(updated, markModified: true);
            await _documentSaver
                .SaveAsync(wstoryPath, updated)
                .ConfigureAwait(true);
            document.IsModified = false;
            await CompileNowAsync().ConfigureAwait(true);
            Status.SetStatus($"已设为入口：{module.ModuleId}");
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, $"设置入口失败：{module.ModuleId}");
            Status.SetStatus($"设置入口失败：{exception.Message}");
        }
    }

    /// <summary>
    /// 生成/更新 .wstory：已有文件只标记未保存，缺失时创建 project.wstory。
    /// Generates or updates .wstory; existing files stay modified for review.
    /// </summary>
    [RelayCommand]
    private async Task GenerateProjectFileAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder))
        {
            Status.SetStatus("没有打开项目");
            return;
        }

        try
        {
            var wstoryPath = FindWStoryPath();

            if (wstoryPath is null)
            {
                wstoryPath = await EnsureWStoryFileAsync().ConfigureAwait(true);

                if (wstoryPath is null)
                {
                    return;
                }

                if (!Editor.OpenFile(wstoryPath))
                {
                    Status.SetStatus("无法打开 .wstory");
                    return;
                }

                await CompileNowAsync().ConfigureAwait(true);
                Status.SetStatus("已生成项目文件");
                return;
            }

            await CompileNowAsync().ConfigureAwait(true);

            if (!Editor.OpenFile(wstoryPath) ||
                Editor.SelectedDocument is not { } document)
            {
                Status.SetStatus("无法打开 .wstory");
                return;
            }

            var version = document.Version;
            var current = document.GetTextSnapshot();
            var generated = await _analysisScheduler
                .GenerateWStoryAsync()
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(generated))
            {
                Status.SetStatus("没有可生成的 .wstory");
                return;
            }

            if (document.Version != version)
            {
                Status.SetStatus("文件已变化，请重试");
                return;
            }

            if (string.Equals(current, generated, StringComparison.Ordinal))
            {
                Status.SetStatus("项目文件已是最新");
                return;
            }

            document.UpdateText(generated, markModified: true);
            await CompileNowAsync().ConfigureAwait(true);
            Status.SetStatus("已生成项目文件（未保存）");
        }
        catch (Exception exception)
        {
            AppLog.Error(exception, "生成项目文件失败");
            Status.SetStatus($"生成项目文件失败：{exception.Message}");
        }
    }

    private async Task<string?> EnsureWStoryFileAsync()
    {
        var existing = FindWStoryPath();

        if (existing is not null)
        {
            return existing;
        }

        await CompileNowAsync().ConfigureAwait(true);
        var generated = await _analysisScheduler
            .GenerateWStoryAsync()
            .ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(generated))
        {
            Status.SetStatus("没有可生成的 .wstory");
            return null;
        }

        var path = Path.Combine(_projectFolder!, "project.wstory");
        await _documentSaver
            .SaveAsync(path, generated)
            .ConfigureAwait(true);
        Explorer.RefreshFromDisk();
        return path;
    }

    private string? FindWStoryPath()
    {
        if (string.IsNullOrWhiteSpace(_projectFolder) ||
            !Directory.Exists(_projectFolder))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(_projectFolder, "*", SearchOption.TopDirectoryOnly)
            .Where(static path => Path.GetExtension(path).Equals(
                ".wstory", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private void NavigateToSource(SourceLocation location, string label)
    {
        if (string.IsNullOrWhiteSpace(location.FilePath))
        {
            return;
        }

        var (line, character) = (1, 1);
        var text = GetDocumentText(location.FilePath);

        if (text is not null)
        {
            (line, character) = DiagnosticPresentation.ResolvePosition(text, location.Span.Start);
        }

        Editor.NavigateTo(location.FilePath, line, character);
        Status.SetStatus($"已定位：{label}");
    }
}
