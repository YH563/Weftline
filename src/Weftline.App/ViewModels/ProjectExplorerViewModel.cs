using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Models;
using Weftline.App.Services;

namespace Weftline.App.ViewModels;

public partial class ProjectExplorerViewModel : ObservableObject
{
    private readonly HashSet<ProjectTreeNodeViewModel> _subscribedNodes = [];
    private readonly IDialogService _dialogs;
    private ProjectTreeNodeViewModel? _root;
    private bool _rebuilding;

    [ObservableProperty]
    private string _rootPath;

    [ObservableProperty]
    private string _rootName;

    [ObservableProperty]
    private ProjectTreeNodeViewModel? _selectedNode;

    public ProjectExplorerViewModel()
        : this(Environment.CurrentDirectory, new DialogService())
    {
    }

    public ProjectExplorerViewModel(IDialogService dialogs)
        : this(Environment.CurrentDirectory, dialogs)
    {
    }

    public ProjectExplorerViewModel(string rootPath)
        : this(rootPath, new DialogService())
    {
    }

    public ProjectExplorerViewModel(string rootPath, IDialogService dialogs)
    {
        _dialogs = dialogs;
        _rootPath = rootPath;
        _rootName = GetRootName(rootPath);
        RefreshCommand = new RelayCommand(Refresh);
        Load(rootPath);
    }

    public ObservableCollection<ProjectTreeNodeViewModel> VisibleNodes { get; } = [];

    public bool HasNodes => VisibleNodes.Count > 0;

    public bool HasSelection => SelectedNode is not null;

    public IRelayCommand RefreshCommand { get; }

    public event EventHandler<string>? FileOpenRequested;

    public event EventHandler<ProjectFileChange>? FileSystemChanged;

    public void RequestFileOpen(string path) => FileOpenRequested?.Invoke(this, path);

    public void Load(string folder)
    {
        RootPath = folder;
        RootName = GetRootName(folder);
        ClearSubscriptions();
        VisibleNodes.Clear();
        _root = null;

        if (Directory.Exists(folder))
        {
            _root = new ProjectTreeNodeViewModel(folder, true, -1);
            _root.IsExpanded = true;
        }

        RebuildVisibleNodes();
    }

    [RelayCommand]
    private async Task NewFileAsync()
    {
        var folder = GetTargetFolder();
        var name = await _dialogs.PromptAsync("新建文件", "请输入文件名", "new.weft").ConfigureAwait(true);

        if (name is null)
        {
            return;
        }

        if (!IsValidName(name, out var error))
        {
            await _dialogs.ShowMessageAsync("无法新建文件", error).ConfigureAwait(true);
            return;
        }

        var path = Path.Combine(folder, name);

        try
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                await _dialogs.ShowMessageAsync("无法新建文件", "同名文件或文件夹已存在。").ConfigureAwait(true);
                return;
            }

            await File.WriteAllTextAsync(path, string.Empty).ConfigureAwait(true);
            ReloadAndSelect(path);
            FileSystemChanged?.Invoke(this, new ProjectFileChange(ProjectFileChangeKind.Created, path));
        }
        catch (Exception exception)
        {
            await _dialogs.ShowMessageAsync("无法新建文件", exception.Message).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        var folder = GetTargetFolder();
        var name = await _dialogs.PromptAsync("新建文件夹", "请输入文件夹名", "New Folder").ConfigureAwait(true);

        if (name is null)
        {
            return;
        }

        if (!IsValidName(name, out var error))
        {
            await _dialogs.ShowMessageAsync("无法新建文件夹", error).ConfigureAwait(true);
            return;
        }

        var path = Path.Combine(folder, name);

        try
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                await _dialogs.ShowMessageAsync("无法新建文件夹", "同名文件或文件夹已存在。").ConfigureAwait(true);
                return;
            }

            Directory.CreateDirectory(path);
            ReloadAndSelect(path);
            FileSystemChanged?.Invoke(this, new ProjectFileChange(ProjectFileChangeKind.Created, path));
        }
        catch (Exception exception)
        {
            await _dialogs.ShowMessageAsync("无法新建文件夹", exception.Message).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        if (SelectedNode is not { } node)
        {
            await _dialogs.ShowMessageAsync("重命名", "请先选择文件或文件夹。").ConfigureAwait(true);
            return;
        }

        var name = await _dialogs.PromptAsync("重命名", "请输入新名称", node.Name).ConfigureAwait(true);

        if (name is null || string.Equals(name, node.Name, StringComparison.Ordinal))
        {
            return;
        }

        if (!IsValidName(name, out var error))
        {
            await _dialogs.ShowMessageAsync("无法重命名", error).ConfigureAwait(true);
            return;
        }

        var parent = Path.GetDirectoryName(node.FullPath);

        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        var newPath = Path.Combine(parent, name);

        try
        {
            if (File.Exists(newPath) || Directory.Exists(newPath))
            {
                await _dialogs.ShowMessageAsync("无法重命名", "同名文件或文件夹已存在。").ConfigureAwait(true);
                return;
            }

            if (node.IsDirectory)
            {
                Directory.Move(node.FullPath, newPath);
            }
            else
            {
                File.Move(node.FullPath, newPath);
            }

            ReloadAndSelect(newPath);
            FileSystemChanged?.Invoke(this, new ProjectFileChange(ProjectFileChangeKind.Renamed, newPath, node.FullPath));
        }
        catch (Exception exception)
        {
            await _dialogs.ShowMessageAsync("无法重命名", exception.Message).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedNode is not { } node)
        {
            await _dialogs.ShowMessageAsync("删除", "请先选择文件或文件夹。").ConfigureAwait(true);
            return;
        }

        var confirmed = await _dialogs
            .ConfirmAsync("删除", $"确定删除“{node.Name}”吗？此操作不可恢复。")
            .ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        var parent = Path.GetDirectoryName(node.FullPath) ?? RootPath;

        try
        {
            if (node.IsDirectory)
            {
                Directory.Delete(node.FullPath, true);
            }
            else
            {
                File.Delete(node.FullPath);
            }

            ReloadAndSelect(parent);
            FileSystemChanged?.Invoke(this, new ProjectFileChange(ProjectFileChangeKind.Deleted, node.FullPath));
        }
        catch (Exception exception)
        {
            await _dialogs.ShowMessageAsync("无法删除", exception.Message).ConfigureAwait(true);
        }
    }

    private void ReloadAndSelect(string? path)
    {
        var expanded = new List<string>();

        if (_root is not null)
        {
            CollectExpandedPaths(_root, expanded);
        }

        Load(RootPath);

        foreach (var folder in expanded.OrderBy(static item => item.Length))
        {
            ExpandTo(folder);
        }

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            ExpandTo(path);
        }
        else
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                ExpandTo(parent);
            }
        }

        SelectedNode = VisibleNodes.FirstOrDefault(node =>
            string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase));
    }

    private void CollectExpandedPaths(ProjectTreeNodeViewModel node, ICollection<string> result)
    {
        foreach (var child in node.Children)
        {
            if (child.IsPlaceholder)
            {
                continue;
            }

            if (child.IsDirectory && child.IsExpanded)
            {
                result.Add(child.FullPath);
            }

            CollectExpandedPaths(child, result);
        }
    }

    private void ExpandTo(string folder)
    {
        if (_root is null)
        {
            return;
        }

        var relative = Path.GetRelativePath(RootPath, folder);

        if (relative == "." || relative.StartsWith("..", StringComparison.Ordinal))
        {
            return;
        }

        var current = _root;

        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var next = current.Children.FirstOrDefault(child =>
                child.IsDirectory && string.Equals(child.Name, segment, StringComparison.OrdinalIgnoreCase));

            if (next is null)
            {
                return;
            }

            next.IsExpanded = true;
            current = next;
        }
    }

    private string GetTargetFolder()
    {
        if (SelectedNode is { IsDirectory: true } folder)
        {
            return folder.FullPath;
        }

        if (SelectedNode is { } file)
        {
            return Path.GetDirectoryName(file.FullPath) ?? RootPath;
        }

        return RootPath;
    }

    private static bool IsValidName(string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "名称不能为空。";
            return false;
        }

        if (name is "." or "..")
        {
            error = "名称不合法。";
            return false;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar))
        {
            error = "名称中包含非法字符。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RebuildVisibleNodes()
    {
        if (_rebuilding)
        {
            return;
        }

        _rebuilding = true;

        try
        {
            var selected = SelectedNode;
            ClearSubscriptions();
            VisibleNodes.Clear();

            if (_root is not null)
            {
                foreach (var child in _root.Children)
                {
                    AddVisibleNode(child);
                }
            }

            SelectedNode = selected is not null && VisibleNodes.Contains(selected) ? selected : null;
            OnPropertyChanged(nameof(HasNodes));
        }
        finally
        {
            _rebuilding = false;
        }
    }

    private void AddVisibleNode(ProjectTreeNodeViewModel node)
    {
        if (node.IsPlaceholder)
        {
            return;
        }

        VisibleNodes.Add(node);
        Subscribe(node);

        if (node.IsExpanded)
        {
            foreach (var child in node.Children)
            {
                AddVisibleNode(child);
            }
        }
    }

    private void Subscribe(ProjectTreeNodeViewModel node)
    {
        if (!_subscribedNodes.Add(node))
        {
            return;
        }

        node.PropertyChanged += OnNodePropertyChanged;
        node.StructureChanged += OnNodeStructureChanged;
    }

    private void ClearSubscriptions()
    {
        foreach (var node in _subscribedNodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            node.StructureChanged -= OnNodeStructureChanged;
        }

        _subscribedNodes.Clear();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectTreeNodeViewModel.IsExpanded) or nameof(ProjectTreeNodeViewModel.CanExpand))
        {
            RebuildVisibleNodes();
        }
    }

    private void OnNodeStructureChanged(object? sender, EventArgs e) => RebuildVisibleNodes();

    partial void OnSelectedNodeChanged(ProjectTreeNodeViewModel? value)
        => OnPropertyChanged(nameof(HasSelection));

    private void Refresh() => Load(RootPath);

    private static string GetRootName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? path : name;
    }
}
