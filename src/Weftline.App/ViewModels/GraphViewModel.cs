using CommunityToolkit.Mvvm.ComponentModel;
using Weftline.Language.Ir;

namespace Weftline.App.ViewModels;

/// <summary>
/// 主界面剧情图面板的状态：当前投影和分析摘要。
/// State for the main-window story graph panel: current projection and analysis summary.
/// </summary>
public partial class GraphViewModel : ObservableObject
{
    [ObservableProperty]
    private GraphProjection? _projection;

    [ObservableProperty]
    private string _statusText = "等待分析";

    /// <summary>
    /// 清空图投影和状态，用于切换项目。
    /// Clears the projection and status, used when switching projects.
    /// </summary>
    public void Reset()
    {
        Projection = null;
        StatusText = "等待分析";
    }
}
