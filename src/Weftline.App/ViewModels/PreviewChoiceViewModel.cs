using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Weftline.App.ViewModels;

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

    public string Label { get; }

    public ICommand Command { get; }
}
