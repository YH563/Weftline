using Avalonia.Controls.Primitives;
using SukiUI.Controls;

namespace Weftline.App.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (OperatingSystem.IsLinux())
        {
            RootCornerRadius = default;
        }
    }
}
