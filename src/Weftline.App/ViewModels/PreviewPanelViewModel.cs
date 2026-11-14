using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.Editor.Services;
using Weftline.Models;
using Weftline.Player;

namespace Weftline.App.ViewModels;

/// <summary>
/// 右侧单文件预览：只读、跟随当前文件，按模块顺序展示正文和分支。
/// </summary>
public partial class PreviewPanelViewModel : ObservableObject, IDisposable
{
    private readonly Dictionary<string, Bitmap?> _imageCache = new(StringComparer.Ordinal);
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

    public PreviewPanelViewModel(Action<SourceLocation, string> navigate)
    {
        _navigate = navigate;
    }

    public ObservableCollection<PreviewModuleViewModel> Modules { get; } = [];

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
            ClearImageCache();
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
        IsEmpty = true;
        IsStale = false;
        EmptyMessage = "打开 .weft 文件后显示预览";
        FileName = string.Empty;
        StatusText = "未打开文件";
    }

    public void Dispose() => ClearImageCache();

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
                    branch,
                    location,
                    $"分支 {module.Id} → {branch.Target}"));
            }
        }

        return new PreviewModuleViewModel(
            module.Id,
            lines,
            branches,
            new RelayCommand(() => _navigate(node.Source, $"模块 {module.Id}")));
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
                => PreviewLineViewModel.Figure(LoadImage(content.Path), content.Path),
            _ => PreviewLineViewModel.Narration(text),
        };
    }

    private PreviewBranchViewModel BuildBranch(
        ExitMode mode,
        RuntimeBranch branch,
        SourceLocation? location,
        string label)
    {
        var details = new List<string>();

        if (branch.Condition is not null)
        {
            details.Add($"when {FormatCondition(branch.Condition)}");
        }

        if (branch.Effects.Count > 0)
        {
            details.Add(
                $"{{ {string.Join(", ", branch.Effects.Select(
                    static effect => $"{effect.Name} = {effect.Value.ToString().ToLowerInvariant()}"))} }}");
        }

        var isChoice = mode == ExitMode.Choice &&
                       !string.IsNullOrWhiteSpace(branch.Label);

        Action? open = null;
        if (location is { } source)
        {
            open = () => _navigate(source, label);
        }

        return new PreviewBranchViewModel(
            isChoice,
            isChoice ? branch.Label! : string.Empty,
            branch.Target,
            details.Count == 0 ? null : string.Join(" · ", details),
            open);
    }

    private static string FormatCondition(IrBoolExpr expression)
    {
        return expression.Kind switch
        {
            "literal" => expression.Value == true ? "true" : "false",
            "ref" => expression.Name ?? string.Empty,
            "not" => $"not ({FormatCondition(expression.Expr ?? IrBoolExpr.Literal(true))})",
            "and" => $"({FormatCondition(expression.Left ?? IrBoolExpr.Literal(true))} and " +
                     $"{FormatCondition(expression.Right ?? IrBoolExpr.Literal(true))})",
            "or" => $"({FormatCondition(expression.Left ?? IrBoolExpr.Literal(true))} or " +
                    $"{FormatCondition(expression.Right ?? IrBoolExpr.Literal(true))})",
            _ => string.Empty,
        };
    }

    private void SetEmpty(string message)
    {
        IsEmpty = true;
        EmptyMessage = message;
        StatusText = string.IsNullOrEmpty(FileName) ? message : $"{FileName} · {message}";
    }

    private Bitmap? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (_imageCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        Bitmap? image = null;

        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(_projectFolder, path));
            var root = Path.TrimEndingDirectorySeparator(_projectFolder);

            if (fullPath.StartsWith(
                    root + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal) &&
                File.Exists(fullPath))
            {
                using var stream = File.OpenRead(fullPath);
                image = new Bitmap(stream);
            }
        }
        catch (Exception)
        {
            image = null;
        }

        _imageCache[path] = image;
        return image;
    }

    private void ClearImageCache()
    {
        foreach (var image in _imageCache.Values)
        {
            image?.Dispose();
        }

        _imageCache.Clear();
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
