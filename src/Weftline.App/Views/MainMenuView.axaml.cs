using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Weftline.App.Controls;

namespace Weftline.App.Views;

public partial class MainMenuView : UserControl
{
    private readonly MenuAutoCloseBehavior _menuAutoClose;

    public MainMenuView()
    {
        InitializeComponent();
        _menuAutoClose = MenuAutoCloseBehavior.Attach(TopMenu);
    }
}

