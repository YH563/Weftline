using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.Player;

namespace Weftline.App.ViewModels;

/// <summary>
/// 应用内剧本预览：逐句推进、选项、图片和局部起点。
/// </summary>
public partial class PreviewViewModel : ObservableObject, IDisposable
{
    private readonly RuntimeProjection _projection;
    private readonly string _projectFolder;
    private readonly Dictionary<string, Bitmap?> _imageCache = new(StringComparer.Ordinal);

    private PlayerSession _session;
    private string _lastChapterTitle = string.Empty;

    [ObservableProperty]
    private string _chapterTitle = string.Empty;

    [ObservableProperty]
    private string _selectedStartModule = string.Empty;

    [ObservableProperty]
    private bool _hasChoices;

    [ObservableProperty]
    private bool _isEnded;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _canAdvance;

    [ObservableProperty]
    private string _hintText = string.Empty;

    [ObservableProperty]
    private bool _isDebugVisible;

    [ObservableProperty]
    private string _debugText = string.Empty;

    public PreviewViewModel(
        RuntimeProjection projection,
        string projectFolder,
        string? startModuleId)
    {
        ArgumentNullException.ThrowIfNull(projection);

        _projection = projection;
        _projectFolder = Path.GetFullPath(projectFolder);

        ProjectName = string.IsNullOrWhiteSpace(projection.ProjectName)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(_projectFolder))
            : projection.ProjectName;

        var validStart = startModuleId;
        if (validStart is not null &&
            projection.Modules.All(module =>
                !string.Equals(module.Id, validStart, StringComparison.Ordinal)))
        {
            validStart = null;
        }

        _session = new PlayerSession(projection, validStart);
        StartModules = BuildStartModules(projection);
        _selectedStartModule = validStart
            ?? projection.Entry
            ?? StartModules.FirstOrDefault()
            ?? string.Empty;

        RestartCommand = new RelayCommand(Restart);
        RestartFromSelectedCommand = new RelayCommand(RestartFromSelected);

        Refresh();
    }

    public string ProjectName { get; }

    public IReadOnlyList<string> StartModules { get; }

    public ObservableCollection<PreviewBlockViewModel> VisibleBlocks { get; } = [];

    public ObservableCollection<PreviewChoiceViewModel> Choices { get; } = [];

    public IRelayCommand RestartCommand { get; }

    public IRelayCommand RestartFromSelectedCommand { get; }

    /// <summary>
    /// 显示下一句；没有可推进内容时忽略。
    /// </summary>
    public void Advance()
    {
        if (_session.Advance())
        {
            Refresh();
        }
    }

    /// <summary>
    /// 选择当前可见选项之一。
    /// </summary>
    public void Choose(int index)
    {
        if (_session.Choose(index))
        {
            Refresh();
        }
    }

    public void Dispose()
    {
        foreach (var image in _imageCache.Values)
        {
            image?.Dispose();
        }

        _imageCache.Clear();
    }

    private void Restart()
    {
        _session.Restart();
        Refresh();
    }

    private void RestartFromSelected()
    {
        var start = string.IsNullOrWhiteSpace(SelectedStartModule)
            ? _projection.Entry
            : SelectedStartModule;

        _session = new PlayerSession(_projection, start);
        Refresh();
    }

    private void Refresh()
    {
        if (_session.CurrentChapterTitle is { } title)
        {
            _lastChapterTitle = title;
        }

        ChapterTitle = _lastChapterTitle;
        IsEnded = _session.IsEnded;
        HasError = _session.HasError;
        ErrorMessage = _session.ErrorMessage ?? string.Empty;
        CanAdvance = _session.CanAdvance;
        HintText = CanAdvance ? "空格 / 回车继续" : string.Empty;

        VisibleBlocks.Clear();
        foreach (var content in _session.VisibleContent)
        {
            VisibleBlocks.Add(CreateBlock(content));
        }

        Choices.Clear();
        for (var index = 0; index < _session.VisibleChoices.Count; index++)
        {
            var branch = _session.VisibleChoices[index];
            Choices.Add(new PreviewChoiceViewModel(
                index,
                branch.Label ?? string.Empty,
                Choose));
        }

        HasChoices = Choices.Count > 0;
        DebugText = BuildDebugText();
    }

    private PreviewBlockViewModel CreateBlock(RuntimeContent content)
    {
        var text = string.Concat(
            content.Segments.Select(static segment => segment.Text));

        return content.Kind switch
        {
            RuntimeContentKind.Dialogue
                => PreviewBlockViewModel.Dialogue(content.Speaker ?? string.Empty, text),
            RuntimeContentKind.Figure
                => PreviewBlockViewModel.Figure(LoadImage(content.Path), content.Path),
            _ => PreviewBlockViewModel.Narration(text),
        };
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

            // 只允许读取项目目录内的资源，避免 @fig 路径越界。
            if (fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && File.Exists(fullPath))
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

    private string BuildDebugText()
    {
        var parts = new List<string>();

        if (_session.CurrentModuleId is { } moduleId)
        {
            parts.Add($"模块 {moduleId}");
        }

        if (_session.CurrentScene is { } scene)
        {
            parts.Add($"场景 {scene}");
        }

        if (_session.Variables.Count > 0)
        {
            var variables = string.Join(
                " · ",
                _session.Variables
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair => $"{pair.Key}={pair.Value}"));
            parts.Add($"变量 {variables}");
        }

        return string.Join(" · ", parts);
    }

    private static IReadOnlyList<string> BuildStartModules(RuntimeProjection projection)
    {
        var modules = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(projection.Entry) &&
            seen.Add(projection.Entry))
        {
            modules.Add(projection.Entry);
        }

        foreach (var module in projection.Modules)
        {
            if (seen.Add(module.Id))
            {
                modules.Add(module.Id);
            }
        }

        return modules;
    }
}
