using Weftline.Core;
using Weftline.Core.Graph.Analysis;

namespace Weftline.App.Features.Graph.Controls;

public sealed partial class StoryGraphView
{
    private GraphModuleNode? FindCurrentModule(string moduleId)
        => Projection?.Modules.FirstOrDefault(module =>
            string.Equals(module.ModuleId, moduleId, StringComparison.Ordinal));

    private GraphModuleEdge FindCurrentModuleEdge(GraphModuleEdge layoutEdge)
    {
        if (Projection is null)
        {
            return layoutEdge;
        }

        return Projection.ModuleEdges.FirstOrDefault(edge =>
            string.Equals(edge.FromModule, layoutEdge.FromModule, StringComparison.Ordinal) &&
            string.Equals(edge.ToModule, layoutEdge.ToModule, StringComparison.Ordinal))
            ?? layoutEdge;
    }

    private ChapterEdge FindCurrentChapterEdge(ChapterEdge layoutEdge)
    {
        if (Projection is null)
        {
            return layoutEdge;
        }

        return Projection.ChapterEdges.FirstOrDefault(edge =>
            string.Equals(edge.FromChapter, layoutEdge.FromChapter, StringComparison.Ordinal) &&
            string.Equals(edge.ToChapter, layoutEdge.ToChapter, StringComparison.Ordinal))
            ?? layoutEdge;
    }

    /// <summary>
    /// 从宿主代码选中一个模块并刷新相关路径高亮。
    /// Selects a module from host code and refreshes related path highlights.
    /// </summary>
    public void SelectModule(string moduleId)
    {
        if (Projection is null ||
            Projection.Modules.All(module => !string.Equals(
                module.ModuleId,
                moduleId,
                StringComparison.Ordinal)))
        {
            return;
        }

        _selectedModuleId = moduleId;
        _selectedChapterId = null;
        _selectedEnd = false;
        UpdatePathHighlight();
        InvalidateVisual();
    }

    /// <summary>
    /// 从宿主代码选中一个章节并刷新相关路径高亮。
    /// Selects a chapter from host code and refreshes related path highlights.
    /// </summary>
    public void SelectChapter(string chapterId)
    {
        if (Projection is null ||
            Projection.Chapters.All(chapter => !string.Equals(
                chapter.ChapterId,
                chapterId,
                StringComparison.Ordinal)))
        {
            return;
        }

        _selectedModuleId = null;
        _selectedChapterId = chapterId;
        _selectedEnd = false;
        UpdatePathHighlight();
        InvalidateVisual();
    }

    private void UpdatePathHighlight()
        => _pathHighlight = GraphPathAnalyzer.Build(
            Projection,
            _selectedModuleId,
            _selectedChapterId);

    private void UpdateFilterMatches()
        => _filterMatch = GraphFilterMatcher.Match(Projection, Filter);
}
