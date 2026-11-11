namespace Weftline.Models;

/// <summary>
/// 图视图的筛选条件；同维度内 OR，跨维度 AND。
/// Graph view filter selection: OR within a dimension, AND across dimensions.
/// </summary>
public sealed record GraphFilterSelection(
    IReadOnlySet<string> Characters,
    IReadOnlySet<string> Scenes,
    IReadOnlySet<string> Variables)
{
    /// <summary>
    /// 空筛选，表示显示全部模块。
    /// Empty selection, meaning all modules are shown.
    /// </summary>
    public static GraphFilterSelection Empty { get; } = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    public bool IsEmpty => Characters.Count == 0
        && Scenes.Count == 0
        && Variables.Count == 0;
}
