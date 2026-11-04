using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Weftline.App.Views;

namespace Weftline.App.Services;

public sealed class DialogService : IDialogService
{
    public async Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
    {
        if (GetOwner()?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var folders = await storageProvider
            .OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            })
            .ConfigureAwait(true);

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PromptAsync(
        string title,
        string message,
        string initialValue = "",
        CancellationToken cancellationToken = default)
    {
        if (GetOwner() is not { } owner)
        {
            return null;
        }

        var window = new TextPromptWindow(title, message, initialValue);
        return await window.ShowDialog<string?>(owner).ConfigureAwait(true);
    }

    public async Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        if (GetOwner() is not { } owner)
        {
            return false;
        }

        var window = new MessageWindow(title, message, showCancel: true);
        return await window.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    public async Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        if (GetOwner() is not { } owner)
        {
            return;
        }

        var window = new MessageWindow(title, message, showCancel: false);
        await window.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private static Window? GetOwner()
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
}
