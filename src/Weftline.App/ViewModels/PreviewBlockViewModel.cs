using Avalonia.Media.Imaging;
using Weftline.Player;

namespace Weftline.App.ViewModels;

/// <summary>
/// 预览窗口中的一块正文：旁白、对话或图片。
/// </summary>
public sealed class PreviewBlockViewModel
{
    private PreviewBlockViewModel(
        RuntimeContentKind kind,
        string text,
        string? speaker,
        Bitmap? image,
        string? imagePath)
    {
        Kind = kind;
        Text = text;
        Speaker = speaker ?? string.Empty;
        Image = image;
        ImagePath = imagePath;
    }

    public RuntimeContentKind Kind { get; }

    public string Text { get; }

    public string Speaker { get; }

    public Bitmap? Image { get; }

    public string? ImagePath { get; }

    public bool IsNarration => Kind == RuntimeContentKind.Narration;

    public bool IsDialogue => Kind == RuntimeContentKind.Dialogue;

    public bool IsImage => Kind == RuntimeContentKind.Figure;

    public bool HasImage => Image is not null;

    public bool IsMissingImage => IsImage && Image is null;

    public static PreviewBlockViewModel Narration(string text)
        => new(RuntimeContentKind.Narration, text, null, null, null);

    public static PreviewBlockViewModel Dialogue(string speaker, string text)
        => new(RuntimeContentKind.Dialogue, text, speaker, null, null);

    public static PreviewBlockViewModel Figure(Bitmap? image, string? imagePath)
        => new(RuntimeContentKind.Figure, string.Empty, null, image, imagePath);
}
