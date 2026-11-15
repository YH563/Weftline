using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Weftline.App.Features.Player.ViewModels;

/// <summary>
/// 预览窗口中的一个选项。
/// </summary>
public sealed class PreviewChoiceViewModel
{
    public PreviewChoiceViewModel(int index, string label, Action<int> choose)
    {
        Index = index;
        Label = label;
        Command = new RelayCommand(() => choose(index));
    }

    public int Index { get; }

    public int Number => Index + 1;

    public string Label { get; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? string.Empty : $"{Number}. {Label}";

    public ICommand Command { get; }
}
