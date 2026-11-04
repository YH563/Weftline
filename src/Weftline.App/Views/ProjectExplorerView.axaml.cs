using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Weftline.App.ViewModels;

namespace Weftline.App.Views;

public partial class ProjectExplorerView : UserControl
{
    public ProjectExplorerView()
    {
        InitializeComponent();
    }


    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is null)
        {
            DataContext = new ProjectExplorerViewModel();
        }
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not ProjectExplorerViewModel ||
            sender is not ListBox listBox ||
            listBox.SelectedItem is not ProjectTreeNodeViewModel node)
        {
            return;
        }

        if (node.IsDirectory)
        {
            node.ToggleExpandCommand.Execute(null);
        }
        else
        {
            ((ProjectExplorerViewModel)DataContext).RequestFileOpen(node.FullPath);
        }
    }
}
