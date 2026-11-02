using Avalonia;
using System;

namespace Weftline.App;

sealed class Program
{
    // 初始化代码。在 AppMain 调用之前，不要使用 Avalonia、第三方 API
    // 或依赖 SynchronizationContext 的代码：此时尚未初始化，可能会出错。
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia 配置，不要删除；可视化设计器也会使用它。
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
