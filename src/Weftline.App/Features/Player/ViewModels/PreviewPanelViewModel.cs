using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Features.Editor.Services;
using Weftline.Core;
using Weftline.Core.Runtime;

namespace Weftline.App.Features.Player.ViewModels;

/// <summary>
/// 右侧单文件预览：只读、跟随当前文件，按模块顺序展示正文和分支。
/// </summary>
public partial class PreviewPanelViewModel : ObservableObject, IDisposable
{
    private readonly PreviewImageCache _images = new();
    private readonly Action<SourceLocation, string> _navigate;

    private RuntimeProjection? _runtime;
    private GraphProjection? _projection;
    private string _projectFolder = string.Empty;
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _emptyMessage = "打开 .weft 文件后显示预览";

    [ObservableProperty]
    private bool _isStale;

    [ObservableProperty]
    private string _statusText = "未打开文件";

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _chapterTitle = string.Empty;

    public PreviewPanelViewModel(Action<SourceLocation, string> navigate)
    {
        _navigate = navigate;
    }

    public ObservableCollection<PreviewModuleViewModel> Modules { get; } = [];

    /// <summary>
    /// 请求预览滚动到指定模块，由视图负责把模块带入可视区域。
    /// </summary>
    public event Action<string>? ModulePreviewRequested;

    /// <summary>
    /// 更新项目编译结果；runtime 为空时保留上一次可用内容，由 IsStale 标记。
    /// </summary>
    public void UpdateProject(
        string projectFolder,
        RuntimeProjection? runtime,
        GraphProjection? projection,
        bool isStale)
    {
        if (!string.Equals(_projectFolder, projectFolder, StringComparison.Ordinal))
        {
            _projectFolder = projectFolder;
            _images.SetProjectFolder(projectFolder);
        }

        _runtime = runtime;
        _projection = projection;
        IsStale = isStale;
        Rebuild();
    }

    /// <summary>
    /// 切换当前预览文件；预览只展示这个文件内的模块。
    /// </summary>
    public void SetFile(string? filePath)
    {
        if (string.Equals(_currentFilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentFilePath = filePath;
        FileName = string.IsNullOrEmpty(filePath) ? string.Empty : Path.GetFileName(filePath);
        Rebuild();
    }

    public void Reset()
    {
        _runtime = null;
        _projection = null;
        _currentFilePath = null;
        Modules.Clear();
        _images.Clear();
        IsEmpty = true;
        IsStale = false;
        EmptyMessage = "打开 .weft 文件后显示预览";
        FileName = string.Empty;
        ChapterTitle = string.Empty;
        StatusText = "未打开文件";
    }

    public void Dispose() => _images.Dispose();

    private void Rebuild()
    {
        Modules.Clear();

        if (_runtime is null || _projection is null)
        {
            SetEmpty(IsStale ? "项目有错误，暂无可用预览" : "等待项目分析…");
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath) ||
            !WeftDocumentKinds.IsWeftFile(_currentFilePath))
        {
            SetEmpty("当前文件不是 .weft 剧本");
            return;
        }

        var projectionById = _projection.Modules.ToDictionary(
            static module => module.ModuleId,
            StringComparer.Ordinal);

        var moduleIds = _projection.Modules
            .Where(module => IsSamePath(module.Source.FilePath, _currentFilePath))
            .Select(static module => module.ModuleId)
            .ToList();

        if (moduleIds.Count == 0)
        {
            SetEmpty("当前文件没有可预览的模块");
            return;
        }

        var runtimeById = _runtime.Modules.ToDictionary(
            static module => module.Id,
            StringComparer.Ordinal);

        var firstRuntimeModule = moduleIds
            .Select(moduleId => runtimeById.GetValueOrDefault(moduleId))
            .FirstOrDefault(static module => module is not null);

        ChapterTitle = firstRuntimeModule is null
            ? string.Empty
            : _runtime.Chapters
                .FirstOrDefault(chapter => string.Equals(
                    chapter.Id,
                    firstRuntimeModule.ChapterId,
                    StringComparison.Ordinal))
                ?.Title
                ?? string.Empty;

        foreach (var moduleId in moduleIds)
        {
            if (runtimeById.TryGetValue(moduleId, out var module) &&
                projectionById.TryGetValue(moduleId, out var node))
            {
                Modules.Add(BuildModule(module, node, projectionById));
            }
        }

        if (Modules.Count == 0)
        {
            SetEmpty("当前文件没有可预览的模块");
            return;
        }

        Modules.Add(PreviewModuleViewModel.End());
        IsEmpty = false;
        StatusText = IsStale
            ? $"{FileName} · {Modules.Count - 1} 模块 · 内容已过期"
            : $"{FileName} · {Modules.Count - 1} 模块";
    }

    private PreviewModuleViewModel BuildModule(
        RuntimeModule module,
        GraphModuleNode node,
        IReadOnlyDictionary<string, GraphModuleNode> projectionById)
    {
        var lines = new List<PreviewLineViewModel>();
        var sceneIndex = 0;

        for (var contentIndex = 0; contentIndex <= module.Content.Count; ++contentIndex)
        {
            while (sceneIndex < module.Scenes.Count &&
                   module.Scenes[sceneIndex].ContentIndex == contentIndex)
            {
                lines.Add(PreviewLineViewModel.Scene(module.Scenes[sceneIndex].Name));
                ++sceneIndex;
            }

            if (contentIndex < module.Content.Count)
            {
                lines.Add(BuildLine(module.Content[contentIndex]));
            }
        }

        var edges = _projection?.ModuleEdges
            .Where(edge => string.Equals(edge.FromModule, module.Id, StringComparison.Ordinal))
            .ToList() ?? [];

        var branches = new List<PreviewBranchViewModel>();
        if (module.Exit is { } exit)
        {
            for (var index = 0; index < exit.Branches.Count; ++index)
            {
                var branch = exit.Branches[index];
                var edge = FindEdge(edges, branch);
                var location = edge?.Source;

                if (location is null &&
                    projectionById.TryGetValue(branch.Target, out var target))
                {
                    location = target.Source;
                }

                branches.Add(BuildBranch(
                    exit.Mode,
                    index + 1,
                    branch,
                    location,
                    $"分支 {module.Id} → {branch.Target}"));
            }
        }

        var moduleExit = module.Exit;
        var choicePrompt = moduleExit is { Mode: ExitMode.Choice }
            ? string.IsNullOrWhiteSpace(moduleExit.Character)
                ? "◇"
                : $"{moduleExit.Character}："
            : string.Empty;
        var hasAutoExit = moduleExit is { Mode: ExitMode.Auto } && moduleExit.Branches.Count > 0;
        var autoExitText = hasAutoExit
            ? moduleExit!.Branches.Any(branch =>
                    string.Equals(branch.Target, "END", StringComparison.Ordinal))
                ? "— 完 —"
                : "↓ 自动继续"
            : string.Empty;

        return new PreviewModuleViewModel(
            module.Id,
            lines,
            branches,
            new RelayCommand(() => _navigate(node.Source, $"模块 {module.Id}")),
            choicePrompt,
            hasAutoExit,
            autoExitText);
    }

    private static GraphModuleEdge? FindEdge(
        IReadOnlyList<GraphModuleEdge> edges,
        RuntimeBranch branch)
    {
        foreach (var edge in edges)
        {
            if (!string.Equals(edge.ToModule, branch.Target, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(branch.Label) &&
                !string.Equals(edge.Label, branch.Label, StringComparison.Ordinal))
            {
                continue;
            }

            return edge;
        }

        return null;
    }

    private PreviewLineViewModel BuildLine(RuntimeContent content)
    {
        var text = string.Concat(content.Segments.Select(static segment => segment.Text));

        return content.Kind switch
        {
            RuntimeContentKind.Dialogue
                => PreviewLineViewModel.Dialogue(content.Speaker ?? string.Empty, text),
            RuntimeContentKind.Figure
                => PreviewLineViewModel.Figure(_images.Get(content.Path), content.Path),
            _ => PreviewLineViewModel.Narration(text),
        };
    }

    private PreviewBranchViewModel BuildBranch(
        ExitMode mode,
        int number,
        RuntimeBranch branch,
        SourceLocation? location,
        string label)
    {
        var isChoice = mode == ExitMode.Choice &&
                       !string.IsNullOrWhiteSpace(branch.Label);

        Action? open = null;
        if (isChoice)
        {
            open = () => NavigateToChoiceTarget(branch.Target, label);
        }
        else if (location is { } source)
        {
            open = () => _navigate(source, label);
        }

        return new PreviewBranchViewModel(
            isChoice,
            number,
            isChoice ? branch.Label! : string.Empty,
            branch.Target,
            open);
    }

    private void NavigateToChoiceTarget(string target, string label)
    {
        if (Modules.Any(module =>
                string.Equals(module.ModuleId, target, StringComparison.Ordinal)))
        {
            ModulePreviewRequested?.Invoke(target);
            return;
        }

        var source = _projection?.Modules
            .FirstOrDefault(module =>
                string.Equals(module.ModuleId, target, StringComparison.Ordinal))
            ?.Source;

        if (source is { } targetSource)
        {
            _navigate(targetSource, label);
        }
    }

    private void SetEmpty(string message)
    {
        IsEmpty = true;
        EmptyMessage = message;
        ChapterTitle = string.Empty;
        StatusText = string.IsNullOrEmpty(FileName) ? message : $"{FileName} · {message}";
    }

    private static bool IsSamePath(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
