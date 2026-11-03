using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Weftline.App.Models;

namespace Weftline.App.ViewModels;

public sealed class ProjectExplorerViewModel
{
    public ProjectExplorerViewModel(IReadOnlyList<SampleChapter> chapters)
    {
        Chapters = new ObservableCollection<ChapterNodeViewModel>(
            chapters.Select(static chapter => new ChapterNodeViewModel(
                chapter.Title,
                new ObservableCollection<ModuleNodeViewModel>(
                    chapter.Modules.Select(static module => new ModuleNodeViewModel(module.Id, module.Scene))))));

        SymbolGroups = new ObservableCollection<SymbolGroupViewModel>
        {
            new("角色", ["萧影", "影子"]),
            new("道具", ["医院钥匙"]),
            new("场景", ["街道", "医院", "病房", "走廊"]),
            new("变量", ["trust_low", "has_key"]),
        };
    }

    public string ProjectName => SampleProject.Name;

    public ObservableCollection<ChapterNodeViewModel> Chapters { get; }

    public ObservableCollection<SymbolGroupViewModel> SymbolGroups { get; }
}

public partial class ChapterNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    public ChapterNodeViewModel(string title, ObservableCollection<ModuleNodeViewModel> modules)
    {
        Title = title;
        Modules = modules;
    }

    public string Title { get; }

    public ObservableCollection<ModuleNodeViewModel> Modules { get; }
}

public sealed class ModuleNodeViewModel
{
    public ModuleNodeViewModel(string id, string scene)
    {
        Id = id;
        Scene = scene;
    }

    public string Id { get; }

    public string Scene { get; }
}

public partial class SymbolGroupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded = true;

    public SymbolGroupViewModel(string title, IReadOnlyList<string> items)
    {
        Title = title;
        Items = new ObservableCollection<string>(items);
    }

    public string Title { get; }

    public ObservableCollection<string> Items { get; }
}
