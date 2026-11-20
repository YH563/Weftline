using Weftline.Core.Runtime;

namespace Weftline.App.Services;

/// <summary>
/// 项目级分析调度抽象：防抖、串行化与取消由实现负责，调用方只订阅事件和读取结果。
/// </summary>
public interface IProjectAnalysisScheduler
{
    /// <summary>一次分析开始（已回到 UI 线程）。</summary>
    event Action? Started;

    /// <summary>一次分析完成（已回到 UI 线程）。</summary>
    event Action<ProjectAnalysisResult>? Completed;

    /// <summary>一次分析失败（已回到 UI 线程）。</summary>
    event Action<Exception>? Failed;

    /// <summary>防抖后调度一次分析。</summary>
    void Schedule(string folder, bool reloadFromDisk, bool buildRuntime = false);

    /// <summary>立即调度一次分析，用于打开项目、保存文件等明确动作。</summary>
    Task RunNowAsync(string folder, bool reloadFromDisk, bool buildRuntime = false);

    /// <summary>用当前编译缓存生成播放器载荷。</summary>
    Task<RuntimeProjection> CompileRuntimeAsync(CancellationToken cancellationToken = default);

    /// <summary>用当前项目生成 .wstory 文本，不写磁盘。</summary>
    Task<string> GenerateWStoryAsync(CancellationToken cancellationToken = default);

    /// <summary>取消当前分析并销毁 native 句柄。</summary>
    Task ResetAsync();

    /// <summary>取消当前分析。</summary>
    void Cancel();
}
