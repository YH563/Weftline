using System.Windows.Input;

namespace Weftline.App.ViewModels;

/// <summary>
/// 文件预览中的一个模块：正文流 + 出口分支。
/// </summary>
public sealed class PreviewModuleViewModel
{
    public PreviewModuleViewModel(
        string moduleId,
        IReadOnlyList<PreviewLineViewModel> lines,
        IReadOnlyList<PreviewBranchViewModel> branches,
        ICommand? openCommand,
        bool isEnd = false)
    {
        ModuleId = moduleId;
        Lines = lines;
        Branches = branches;
        OpenCommand = openCommand;
        IsEnd = isEnd;
    }

    public string ModuleId { get; }

    public IReadOnlyList<PreviewLineViewModel> Lines { get; }

    public IReadOnlyList<PreviewBranchViewModel> Branches { get; }

    public bool HasBranches => Branches.Count > 0;

    public string BranchSectionTitle => Branches.Any(static branch => branch.IsChoice)
        ? "选择"
        : "继续";

    public ICommand? OpenCommand { get; }

    public bool IsEnd { get; }

    public static PreviewModuleViewModel End()
        => new("END", [], [], null, isEnd: true);
}
