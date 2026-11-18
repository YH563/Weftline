using Avalonia.Threading;

namespace Weftline.App.Services;

/// <summary>
/// "回到 UI 线程"的抽象。
/// 调度器依赖它而不是直接调用 Avalonia Dispatcher，测试可以换成同步实现。
/// </summary>
public interface IUiDispatcher
{
    Task InvokeAsync(Action action);

    Task<T> InvokeAsync<T>(Func<T> function);
}

/// <summary>
/// 生产实现：把操作投递到 Avalonia UI 线程，并用 Task 暴露完成/异常。
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Action action)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        return completion.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> function)
    {
        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                completion.SetResult(function());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });

        return completion.Task;
    }
}
