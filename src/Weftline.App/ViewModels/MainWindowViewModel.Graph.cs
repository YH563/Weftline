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
