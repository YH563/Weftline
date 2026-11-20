using Weftline.Core.Runtime;

namespace Weftline.Native;

/// <summary>
/// 项目编译产物选择；与 native core.h 中的 WEFT_COMPILE_* 位保持一致。
/// </summary>
[Flags]
public enum NativeCompileFlags : uint
{
    None = 0,
    Partial = 1u << 0,
    Projection = 1u << 1,
    Runtime = 1u << 2,
    WStory = 1u << 3,
}

/// <summary>
/// 项目句柄的最小抽象。
/// 调度器依赖它而不是 <see cref="NativeWeftlineClient"/>，以便在测试或没有原生库的环境替换实现。
/// </summary>
public interface INativeProjectClient : IDisposable
{
    /// <summary>native core 的版本号。</summary>
    string Version { get; }

    /// <summary>
    /// 同步顶层 .weft/.wstory：打开文档优先，未打开文件按时间戳增量读取。
    /// </summary>
    IReadOnlyDictionary<string, string> LoadFolder(
        string folder,
        IReadOnlyDictionary<string, string> openDocuments,
        bool reloadFromDisk = false);

    /// <summary>编译当前项目并反序列化 native 响应。</summary>
    NativeProjectResponse Compile(NativeCompileFlags flags);

    /// <summary>严格编译并返回播放器载荷；有 error 时抛异常。</summary>
    RuntimeProjection CompileRuntime();
}
