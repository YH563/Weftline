using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Weftline.App.Views;

namespace Weftline.App.Tests;

public sealed class DialogWindowTests
{
    [Fact]
    public async Task DialogWindowsLoadTheirXaml()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(Weftline.App.App));

        await session.Dispatch(() =>
        {
            var message = new MessageWindow("标题", "消息", showCancel: true);
            var messageText = message.FindControl<TextBlock>("MessageText");
            var cancelButton = message.FindControl<Button>("CancelButton");

            Assert.NotNull(messageText);
            Assert.NotNull(cancelButton);
            Assert.Equal("消息", messageText.Text);
            Assert.True(cancelButton.IsVisible);

            var prompt = new TextPromptWindow("标题", "输入", "初始值");
            var input = prompt.FindControl<TextBox>("InputBox");

            Assert.NotNull(input);
            Assert.Equal("初始值", input.Text);

            message.Close();
            prompt.Close();
            Dispatcher.UIThread.RunJobs();
            return true;
        }, CancellationToken.None);
    }
}
