using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.Models;

namespace Weftline.App.ViewModels;

/// <summary>
/// 一条章节边明细的展示文本。
/// Display text for one underlying module branch of a chapter edge.
/// </summary>
public sealed record ChapterEdgeDetailItem(string Text);

/// <summary>
/// 主界面剧情图面板的状态：当前投影、悬停摘要、章节边明细和筛选条件。
/// State for the main-window story graph panel: projection, hover summary,
/// chapter-edge details, and filters.
/// </summary>
public partial class GraphViewModel : ObservableObject
{
    private bool _rebuildingFilters;

    [ObservableProperty]
    private GraphProjection? _projection;

    [ObservableProperty]
    private string _statusText = "等待分析";

    [ObservableProperty]
    private bool _isHoverSummaryVisible;

    [ObservableProperty]
    private string _hoverTitle = string.Empty;

    [ObservableProperty]
    private string? _hoverFirstLine;

    [ObservableProperty]
    private string _hoverMeta = string.Empty;

    [ObservableProperty]
    private string? _hoverDescription;

    [ObservableProperty]
    private bool _isChapterEdgeDetailsVisible;

    [ObservableProperty]
    private string _chapterEdgeTitle = string.Empty;

    [ObservableProperty]
    private string _chapterEdgeSubtitle = string.Empty;

    [ObservableProperty]
    private GraphFilterSelection _filter = GraphFilterSelection.Empty;

    [ObservableProperty]
    private bool _hasActiveFilter;

    public ObservableCollection<ChapterEdgeDetailItem> ChapterEdgeDetails { get; } = [];

    public string FilterButtonText => HasActiveFilter ? "筛选 · 已启用" : "筛选";

    public ObservableCollection<GraphFilterOption> CharacterOptions { get; } = [];

    public ObservableCollection<GraphFilterOption> SceneOptions { get; } = [];

    public ObservableCollection<GraphFilterOption> VariableOptions { get; } = [];

    public bool HasHoverFirstLine => !string.IsNullOrWhiteSpace(HoverFirstLine);

    public bool HasHoverDescription => !string.IsNullOrWhiteSpace(HoverDescription);

    /// <summary>
    /// 清空全部筛选条件。
    /// Clears all filter selections.
    /// </summary>
    [RelayCommand]
    private void ClearFilter()
    {
        foreach (var option in EnumerateOptions())
        {
            option.IsSelected = false;
        }
    }

    partial void OnProjectionChanged(GraphProjection? value)
        => RebuildFilterOptions(value);

    private void RebuildFilterOptions(GraphProjection? projection)
    {
        var characterNames = projection?.References.Characters.Keys;
        var sceneNames = projection?.References.Scenes.Keys;
        var variableNames = projection?.References.Variables.Keys;

        if (SameNames(CharacterOptions, characterNames) &&
            SameNames(SceneOptions, sceneNames) &&
            SameNames(VariableOptions, variableNames))
        {
            UpdateFilter();
            return;
        }

        var selectedCharacters = SelectedNames(CharacterOptions);
        var selectedScenes = SelectedNames(SceneOptions);
        var selectedVariables = SelectedNames(VariableOptions);

        _rebuildingFilters = true;

        foreach (var option in EnumerateOptions())
        {
            option.PropertyChanged -= OnFilterOptionChanged;
        }

        CharacterOptions.Clear();
        SceneOptions.Clear();
        VariableOptions.Clear();

        AddOptions(CharacterOptions, characterNames, selectedCharacters);
        AddOptions(SceneOptions, sceneNames, selectedScenes);
        AddOptions(VariableOptions, variableNames, selectedVariables);

        _rebuildingFilters = false;
        UpdateFilter();
    }

    private static bool SameNames(
        IReadOnlyCollection<GraphFilterOption> options,
        IEnumerable<string>? names)
    {
        var set = names is null
            ? []
            : names.ToHashSet(StringComparer.Ordinal);

        return options.Count == set.Count &&
            options.All(option => set.Contains(option.Name));
    }

    private void AddOptions(
        ObservableCollection<GraphFilterOption> target,
        IEnumerable<string>? names,
        IReadOnlySet<string> selected)
    {
        if (names is null)
        {
            return;
        }

        foreach (var name in names.OrderBy(static name => name, StringComparer.Ordinal))
        {
            var option = new GraphFilterOption(name, selected.Contains(name));
            option.PropertyChanged += OnFilterOptionChanged;
            target.Add(option);
        }
    }

    private void OnFilterOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_rebuildingFilters &&
            e.PropertyName == nameof(GraphFilterOption.IsSelected))
        {
            UpdateFilter();
        }
    }

    private void UpdateFilter()
    {
        var characters = SelectedNames(CharacterOptions);
        var scenes = SelectedNames(SceneOptions);
        var variables = SelectedNames(VariableOptions);

        Filter = new GraphFilterSelection(characters, scenes, variables);
        HasActiveFilter = characters.Count > 0
            || scenes.Count > 0
            || variables.Count > 0;
    }

    private IEnumerable<GraphFilterOption> EnumerateOptions()
        => CharacterOptions.Concat(SceneOptions).Concat(VariableOptions);

    private static HashSet<string> SelectedNames(
        IEnumerable<GraphFilterOption> options)
        => options
            .Where(static option => option.IsSelected)
            .Select(static option => option.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 清空图投影和状态，用于切换项目。
    /// Clears the projection and status, used when switching projects.
    /// </summary>
    public void Reset()
    {
        Projection = null;
        StatusText = "等待分析";
        ClearHover();
        CloseChapterEdgeDetails();
    }

    /// <summary>
    /// 更新悬停模块摘要。
    /// Updates the hover summary for a module.
    /// </summary>
    public void ShowHover(string title, string? firstLine, string meta, string? description)
    {
        HoverTitle = title;
        HoverFirstLine = string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
        HoverMeta = meta;
        HoverDescription = string.IsNullOrWhiteSpace(description) ? null : description;
        IsHoverSummaryVisible = true;
    }

    /// <summary>
    /// 隐藏悬停模块摘要。
    /// Hides the hover summary.
    /// </summary>
    public void ClearHover()
    {
        IsHoverSummaryVisible = false;
        HoverTitle = string.Empty;
        HoverFirstLine = null;
        HoverMeta = string.Empty;
        HoverDescription = null;
    }

    /// <summary>
    /// 显示章节边下所有底层模块分支。
    /// Shows all underlying module branches for a chapter edge.
    /// </summary>
    public void ShowChapterEdgeDetails(
        string title,
        string subtitle,
        IReadOnlyList<ChapterEdgeDetail> details)
    {
        ChapterEdgeTitle = title;
        ChapterEdgeSubtitle = subtitle;
        ChapterEdgeDetails.Clear();

        foreach (var detail in details)
        {
            var mode = detail.Mode == ExitMode.Choice ? "选择" : "自动";
            var label = string.IsNullOrWhiteSpace(detail.Label)
                ? string.Empty
                : $" · {detail.Label}";
            var condition = detail.Condition is null
                ? string.Empty
                : $" · 条件 {FormatCondition(detail.Condition)}";

            ChapterEdgeDetails.Add(new ChapterEdgeDetailItem(
                $"{detail.FromModule} → {detail.ToModule} · {mode}{label}{condition}"));
        }

        IsChapterEdgeDetailsVisible = true;
    }

    private static string FormatCondition(IrBoolExpr expression)
        => expression.Kind switch
        {
            "literal" => expression.Value == true ? "true" : "false",
            "ref" => expression.Name ?? "?",
            "not" when expression.Expr is not null
                => $"!{FormatCondition(expression.Expr)}",
            "and" when expression.Left is not null && expression.Right is not null
                => $"{FormatCondition(expression.Left)} && {FormatCondition(expression.Right)}",
            "or" when expression.Left is not null && expression.Right is not null
                => $"{FormatCondition(expression.Left)} || {FormatCondition(expression.Right)}",
            _ => "?",
        };

    [RelayCommand]
    private void CloseChapterEdgeDetails() => IsChapterEdgeDetailsVisible = false;

    partial void OnHoverFirstLineChanged(string? value)
        => OnPropertyChanged(nameof(HasHoverFirstLine));

    partial void OnHoverDescriptionChanged(string? value)
        => OnPropertyChanged(nameof(HasHoverDescription));

    partial void OnHasActiveFilterChanged(bool value)
        => OnPropertyChanged(nameof(FilterButtonText));
}
