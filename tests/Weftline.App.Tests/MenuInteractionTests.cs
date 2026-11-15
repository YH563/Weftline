using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Weftline.App.Controls;

namespace Weftline.App.Tests;

public sealed class MenuInteractionTests
{
    [Fact]
    public async Task TopLevelPopupPassesPointerEventsToMenu()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(Weftline.App.App));

        await session.Dispatch(() =>
        {
            var file = new MenuItem { Header = "File" };
            file.Items.Add(new MenuItem { Header = "New" });
            var edit = new MenuItem { Header = "Edit" };
            edit.Items.Add(new MenuItem { Header = "Undo" });

            var menu = new Menu { VerticalAlignment = VerticalAlignment.Top };
            menu.Items.Add(file);
            menu.Items.Add(edit);
            using var behavior = MenuAutoCloseBehavior.Attach(menu);

            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = menu,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var filePoint = ToWindow(file, window);
            window.MouseMove(filePoint, RawInputModifiers.None);
            window.MouseDown(filePoint, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(filePoint, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(file.IsSubMenuOpen);

            var popup = file.GetVisualDescendants()
                .OfType<Popup>()
                .FirstOrDefault(popup => popup.Name == "PART_Popup");

            Assert.NotNull(popup);
            Assert.Same(menu, popup.OverlayInputPassThroughElement);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task MovingPointerAwayClosesOpenMenu()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(Weftline.App.App));

        await session.Dispatch(() =>
        {
            var file = new MenuItem { Header = "File" };
            file.Items.Add(new MenuItem { Header = "New" });
            var edit = new MenuItem { Header = "Edit" };
            edit.Items.Add(new MenuItem { Header = "Undo" });

            var menu = new Menu { VerticalAlignment = VerticalAlignment.Top };
            menu.Items.Add(file);
            menu.Items.Add(edit);
            using var behavior = MenuAutoCloseBehavior.Attach(menu);

            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = menu,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var filePoint = ToWindow(file, window);
            window.MouseMove(filePoint, RawInputModifiers.None);
            window.MouseDown(filePoint, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(filePoint, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(file.IsSubMenuOpen);

            window.MouseMove(new Point(350, 150), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            for (var index = 0; index < 8; index++)
            {
                Thread.Sleep(100);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.False(file.IsSubMenuOpen);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task PointerInsideOpenPopupKeepsMenuOpen()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(Weftline.App.App));

        await session.Dispatch(() =>
        {
            var file = new MenuItem { Header = "File" };
            file.Items.Add(new MenuItem { Header = "New" });
            var edit = new MenuItem { Header = "Edit" };
            edit.Items.Add(new MenuItem { Header = "Undo" });

            var menu = new Menu { VerticalAlignment = VerticalAlignment.Top };
            menu.Items.Add(file);
            menu.Items.Add(edit);
            using var behavior = MenuAutoCloseBehavior.Attach(menu);

            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = menu,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var filePoint = ToWindow(file, window);
            window.MouseMove(filePoint, RawInputModifiers.None);
            window.MouseDown(filePoint, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(filePoint, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            var popup = file.GetVisualDescendants()
                .OfType<Popup>()
                .FirstOrDefault(popup => popup.Name == "PART_Popup");
            Assert.NotNull(popup?.Child);

            var popupCenter = popup.Child.TranslatePoint(
                new Point(popup.Child.Bounds.Width / 2, popup.Child.Bounds.Height / 2),
                window) ?? filePoint;

            window.MouseMove(popupCenter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            for (var index = 0; index < 8; index++)
            {
                Thread.Sleep(100);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.True(file.IsSubMenuOpen);

            window.Close();
            return true;
        }, CancellationToken.None);
    }

    private static Point ToWindow(Control control, Window window)
    {
        var center = new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
        return control.TranslatePoint(center, window) ?? center;
    }
}
