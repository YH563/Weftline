namespace Weftline.App.Services;

public interface IDialogService
{
    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default);

    Task<string?> PromptAsync(
        string title,
        string message,
        string initialValue = "",
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default);

    Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default);
}
