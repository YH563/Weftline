using Avalonia.Media.Imaging;

namespace Weftline.App.ViewModels;

public enum PreviewLineKind
{
    Scene,
    Narration,
    Dialogue,
    Image,
}

/// <summary>
/// 文件预览中的一行：场景标题、旁白、对话或图片。
/// </summary>
public sealed class PreviewLineViewModel
{
    private PreviewLineViewModel(
        PreviewLineKind kind,
        string text,
        string speaker,
        Bitmap? image,
        string? imagePath)
    {
        Kind = kind;
        Text = text;
        Speaker = speaker;
        Image = image;
        ImagePath = imagePath;
    }

    public PreviewLineKind Kind { get; }

    public string Text { get; }

    public string Speaker { get; }

    public Bitmap? Image { get; }

    public string? ImagePath { get; }

    public bool IsScene => Kind == PreviewLineKind.Scene;

    public string SceneText => IsScene ? $"— {Text} —" : string.Empty;

    public bool IsNarration => Kind == PreviewLineKind.Narration;

    public bool IsDialogue => Kind == PreviewLineKind.Dialogue;

    public bool IsImage => Kind == PreviewLineKind.Image;

    public bool HasImage => Image is not null;

    public bool IsMissingImage => IsImage && Image is null;

    public static PreviewLineViewModel Scene(string name)
        => new(PreviewLineKind.Scene, name, string.Empty, null, null);

    public static PreviewLineViewModel Narration(string text)
        => new(PreviewLineKind.Narration, text, string.Empty, null, null);

    public static PreviewLineViewModel Dialogue(string speaker, string text)
        => new(PreviewLineKind.Dialogue, text, speaker, null, null);

    public static PreviewLineViewModel Figure(Bitmap? image, string? path)
        => new(PreviewLineKind.Image, string.Empty, string.Empty, image, path);
}
