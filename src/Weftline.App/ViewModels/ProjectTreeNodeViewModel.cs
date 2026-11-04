using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Weftline.App.Services;

namespace Weftline.App.ViewModels;

public partial class ProjectTreeNodeViewModel : ObservableObject
{
    private bool _childrenLoaded;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private double _chevronAngle;

    [ObservableProperty]
    private Geometry _iconGeometry;

    [ObservableProperty]
    private IBrush _iconBrush;

    public ProjectTreeNodeViewModel(string fullPath, bool isDirectory, int level = 0)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Level = level;
        Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (string.IsNullOrEmpty(Name))
        {
            Name = fullPath;
        }

        var icon = FileIconResolver.Resolve(Name, IsDirectory, false);
        _iconGeometry = icon.Geometry;
        _iconBrush = icon.Brush;
        ToggleExpandCommand = new RelayCommand(ToggleExpand);

        if (IsDirectory && HasVisibleEntries(fullPath))
        {
            Children.Add(CreatePlaceholder(level + 1));
        }
    }

    private ProjectTreeNodeViewModel()
    {
        FullPath = string.Empty;
        Name = string.Empty;
        IsPlaceholder = true;
        ToggleExpandCommand = new RelayCommand(static () => { });
        var icon = FileIconResolver.Resolve(string.Empty, false, false);
        _iconGeometry = icon.Geometry;
        _iconBrush = icon.Brush;
    }

    public string Name { get; }

    public string FullPath { get; }

    public bool IsDirectory { get; }

    public bool IsPlaceholder { get; }

    public int Level { get; }

    public double Indent => Math.Max(0, Level) * 14;

    public bool CanExpand => IsDirectory && Children.Count > 0;

    public ObservableCollection<ProjectTreeNodeViewModel> Children { get; } = [];

    public IRelayCommand ToggleExpandCommand { get; }

    public event EventHandler? StructureChanged;

    partial void OnIsExpandedChanged(bool value)
    {
        var icon = FileIconResolver.Resolve(Name, IsDirectory, value);
        IconGeometry = icon.Geometry;
        IconBrush = icon.Brush;
        ChevronAngle = value ? 90 : 0;

        if (value && IsDirectory && !_childrenLoaded)
        {
            LoadChildren();
        }

        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    public void LoadChildren()
    {
        _childrenLoaded = true;
        Children.Clear();

        if (Directory.Exists(FullPath))
        {
            try
            {
                var directories = Directory
                    .EnumerateDirectories(FullPath)
                    .Where(static path => !Path.GetFileName(path).StartsWith('.'))
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);

                foreach (var directory in directories)
                {
                    Children.Add(new ProjectTreeNodeViewModel(directory, true, Level + 1));
                }

                var files = Directory
                    .EnumerateFiles(FullPath)
                    .Where(static path => !Path.GetFileName(path).StartsWith('.'))
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    Children.Add(new ProjectTreeNodeViewModel(file, false, Level + 1));
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        OnPropertyChanged(nameof(CanExpand));
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleExpand()
    {
        if (CanExpand)
        {
            IsExpanded = !IsExpanded;
        }
    }

    private static bool HasVisibleEntries(string folder)
    {
        try
        {
            return Directory
                .EnumerateFileSystemEntries(folder)
                .Any(static path => !Path.GetFileName(path).StartsWith('.'));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ProjectTreeNodeViewModel CreatePlaceholder(int level) => new();
}
