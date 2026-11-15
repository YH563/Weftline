using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Weftline.App.Features.Player.ViewModels;

/// <summary>
/// 文件预览中的一条出口分支。
/// </summary>
public sealed class PreviewBranchViewModel
{
    public PreviewBranchViewModel(
        bool isChoice,
        int number,
        string label,
        string target,
        Action? open)
    {
        IsChoice = isChoice;
        Number = number;
        Label = label;
        Target = target;
        OpenCommand = open is null ? null : new RelayCommand(open);
    }

    public bool IsChoice { get; }

    public bool IsAuto => !IsChoice;

    public int Number { get; }

    public string Label { get; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? string.Empty : $"{Number}. {Label}";

    public string Target { get; }

    public ICommand? OpenCommand { get; }
}
