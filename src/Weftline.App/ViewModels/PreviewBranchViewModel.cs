using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Weftline.App.ViewModels;

/// <summary>
/// 文件预览中的一条出口分支。
/// </summary>
public sealed class PreviewBranchViewModel
{
    public PreviewBranchViewModel(
        bool isChoice,
        string label,
        string target,
        string? detail,
        Action? open)
    {
        IsChoice = isChoice;
        Label = label;
        Target = target;
        Detail = detail;
        OpenCommand = open is null ? null : new RelayCommand(open);
    }

    public bool IsChoice { get; }

    public bool IsAuto => !IsChoice;

    public string Label { get; }

    public string Target { get; }

    public string? Detail { get; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    public string TargetText => $"→ {Target}";

    public ICommand? OpenCommand { get; }
}
