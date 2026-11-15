using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Weftline.App.Features.Player.ViewModels;

/// <summary>
/// 文件预览中的一个模块：正文流 + 出口标记。
/// </summary>
public sealed partial class PreviewModuleViewModel : ObservableObject
{
    public PreviewModuleViewModel(
        string moduleId,
        IReadOnlyList<PreviewLineViewModel> lines,
        IReadOnlyList<PreviewBranchViewModel> branches,
        ICommand? openCommand,
        string choicePrompt = "",
        bool hasAutoExit = false,
        string autoExitText = "",
        bool isEnd = false)
    {
        ModuleId = moduleId;
        Lines = lines;
        Branches = branches;
        Choices = branches.Where(static branch => branch.IsChoice).ToList();
        OpenCommand = openCommand;
        ChoicePrompt = choicePrompt;
        HasAutoExit = hasAutoExit;
        AutoExitText = autoExitText;
        IsEnd = isEnd;
    }

    public string ModuleId { get; }

    public IReadOnlyList<PreviewLineViewModel> Lines { get; }

    public IReadOnlyList<PreviewBranchViewModel> Branches { get; }

    public IReadOnlyList<PreviewBranchViewModel> Choices { get; }

    public bool HasChoices => Choices.Count > 0;

    public string ChoicePrompt { get; }

    public bool HasAutoExit { get; }

    public string AutoExitText { get; }

    public ICommand? OpenCommand { get; }

    public bool IsEnd { get; }

    [ObservableProperty]
    private bool _isCurrent;

    public static PreviewModuleViewModel End()
        => new("END", [], [], null, isEnd: true);
}
