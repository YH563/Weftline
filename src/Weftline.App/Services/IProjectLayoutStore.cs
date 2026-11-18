using Weftline.App.Features.Graph.Layout;

namespace Weftline.App.Services;

/// <summary>
/// 剧情图布局侧车（weftline.layout.json）的读写。
/// </summary>
public interface IProjectLayoutStore
{
    /// <summary>读取布局；文件不存在时返回 null，文件损坏时抛出异常。</summary>
    GraphLayoutSnapshot? Load(string folder);

    /// <summary>写入布局，必要时创建文件。</summary>
    void Save(string folder, GraphLayoutSnapshot snapshot);

    /// <summary>删除布局侧车；文件不存在时为空操作。</summary>
    void Clear(string folder);
}
