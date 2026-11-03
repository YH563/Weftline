using CommunityToolkit.Mvvm.ComponentModel;

namespace Weftline.App.ViewModels;

public partial class DocumentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _filePath;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _isDirty;

    public DocumentViewModel(string title, string filePath, string text)
    {
        _title = title;
        _filePath = filePath;
        _text = text;
    }
}
