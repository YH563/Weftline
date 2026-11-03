using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SukiUI;
using Weftline.App.Models;
using Weftline.App.Services;

namespace Weftline.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private DocumentViewModel _selectedDocument;

    [ObservableProperty]
    private string _statusText = "就绪 · 示例项目";

    [ObservableProperty]
    private string _themeLabel = "主题";

    public MainWindowViewModel()
    {
        var document = new DocumentViewModel(
            "第一章 雨夜.weft",
            "samples/believe.weft",
            SampleProject.WeftSample);

        Documents = new ObservableCollection<DocumentViewModel> { document };
        SelectedDocument = document;
        Explorer = new ProjectExplorerViewModel(SampleProject.Chapters);
        Graph = new GraphViewModel(SampleProject.Chapters, SampleProject.Edges);
        Problems = new ObservableCollection<ProblemViewModel>(
            SampleProject.Problems.Select(static problem => new ProblemViewModel(problem)));

        ThemeLabel = ThemeService.Current.IsDark ? "浅色" : "深色";
        ThemeService.Changed += OnThemeChanged;
    }

    public string ProjectName => SampleProject.Name;

    public ObservableCollection<DocumentViewModel> Documents { get; }

    public ProjectExplorerViewModel Explorer { get; }

    public GraphViewModel Graph { get; }

    public ObservableCollection<ProblemViewModel> Problems { get; }

    public string DiagnosticsSummary => $"{Problems.Count} 条诊断";

    [RelayCommand]
    private void ToggleTheme()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var next = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        app.RequestedThemeVariant = next;
        SukiTheme.GetInstance().ChangeBaseTheme(next);
        ThemeService.Apply(next);
    }

    private void OnThemeChanged(object? sender, EventArgs e)
        => ThemeLabel = ThemeService.Current.IsDark ? "浅色" : "深色";

    [RelayCommand]
    private void OpenFolder() => StatusText = "打开文件夹：Phase 1 接入";

    [RelayCommand]
    private void Save() => StatusText = "保存：Phase 1 接入";

    [RelayCommand]
    private void Sync() => StatusText = "同步 .wstory：Phase 1 接入";

    [RelayCommand]
    private void Validate() => StatusText = "校验：Phase 1 接入";
}
