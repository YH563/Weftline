using CommunityToolkit.Mvvm.ComponentModel;

namespace Weftline.App.ViewModels;

/// <summary>
/// 筛选面板中的一个可选符号。
/// One selectable symbol in the filter flyout.
/// </summary>
public partial class GraphFilterOption : ObservableObject
{
    public GraphFilterOption(string name, bool isSelected)
    {
        Name = name;
        IsSelected = isSelected;
    }

    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;
}
