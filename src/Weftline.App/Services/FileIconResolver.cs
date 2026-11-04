using Avalonia.Media;

namespace Weftline.App.Services;

public sealed record FileIconInfo(Geometry Geometry, IBrush Brush);

public static class FileIconResolver
{
    private static readonly FileIconInfo FolderClosed = Create(
        "M10 4H4C2.9 4 2 4.9 2 6V18C2 19.1 2.9 20 4 20H20C21.1 20 22 19.1 22 18V8C22 6.9 21.1 6 20 6H12L10 4Z",
        "#D99A2B");

    private static readonly FileIconInfo FolderOpen = Create(
        "M19 20H4C2.9 20 2 19.1 2 18V6C2 4.9 2.9 4 4 4H10L12 6H20C21.1 6 22 6.9 22 8V10H4V18L6.5 11H23L19 20Z",
        "#E8AE45");

    private static readonly FileIconInfo File = Create(
        "M13 9V3.5L18.5 9H13M6 2C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V8L14 2H6Z",
        "#94A3B8");

    private static readonly FileIconInfo Document = Create(
        "M13 9V3.5L18.5 9H13M6 2C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V8L14 2H6M6 4H13V9H18V20H6V4Z",
        "#64748B");

    private static readonly FileIconInfo Code = Create(
        "M14 2H6C4.89 2 4 2.9 4 4V20C4 21.1 4.89 22 6 22H18C19.11 22 20 21.1 20 20V8L14 2M14 7V3.5L18.5 8H14M9.5 12L7.5 14L9.5 16L8.5 17L5.5 14L8.5 11L9.5 12M14.5 12L17.5 15L14.5 18L13.5 17L15.5 15L13.5 13L14.5 12Z",
        "#7C3AED");

    private static readonly FileIconInfo Script = Create(
        "M18 2H6C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V4C20 2.9 19.1 2 18 2M9 4H11V9L10 8L9 9V4M6 6H8V18H6V6M14 6H18V8H14V6M14 10H18V12H14V10M14 14H18V16H14V14Z",
        "#2563EB");

    private static readonly FileIconInfo Book = Create(
        "M21 4H3C1.9 4 1 4.9 1 6V18C1 19.1 1.9 20 3 20H10.5L12 21.5L13.5 20H21C22.1 20 23 19.1 23 18V6C23 4.9 22.1 4 21 4M21 18H13L12 19L11 18H3V6H21V18Z",
        "#0F766E");

    private static readonly FileIconInfo Braces = Create(
        "M8 3C6.9 3 6 3.9 6 5V7C6 8.1 5.1 9 4 9H3V11H4C5.1 11 6 11.9 6 13V15C6 16.1 6.9 17 8 17H9V15H8C7.4 15 7 14.6 7 14V11.5C7 10.4 6.3 9.5 5.4 9C6.3 8.5 7 7.6 7 6.5V4C7 3.4 7.4 3 8 3H9V1H8M16 3H15V1H16C17.1 1 18 1.9 18 3V5C18 6.1 18.9 7 20 7H21V9H20C18.9 9 18 9.9 18 11V13C18 14.1 17.1 15 16 15H15V17H16C17.1 17 18 17.9 18 19V21H16C14.9 21 14 20.1 14 19V17H13V15H14V12.5C14 11.4 14.7 10.5 15.6 10C14.7 9.5 14 8.6 14 7.5V5C14 3.9 14.9 3 16 3Z",
        "#D97706");

    private static readonly FileIconInfo Image = Create(
        "M8.5 13.5L11 16.5L14.5 12L19 18H5L8.5 13.5M21 4H3C1.89 4 1 4.89 1 6V18C1 19.11 1.89 20 3 20H21C22.11 20 23 19.11 23 18V6C23 4.89 22.11 4 21 4M21 18H3V6H21V18Z",
        "#16A34A");

    private static readonly Dictionary<string, FileIconInfo> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".weft"] = Script,
        [".wstory"] = Book,
        [".cs"] = Code,
        [".axaml"] = Code,
        [".xaml"] = Code,
        [".json"] = Braces,
        [".md"] = Document,
        [".txt"] = Document,
        [".png"] = Image,
        [".jpg"] = Image,
        [".jpeg"] = Image,
        [".gif"] = Image,
        [".webp"] = Image,
        [".svg"] = Image,
        [".html"] = Document,
        [".htm"] = Document,
        [".css"] = Code,
        [".js"] = Code,
        [".ts"] = Code,
        [".csproj"] = File,
        [".sln"] = File,
        [".slnx"] = File,
        [".props"] = File,
        [".targets"] = File,
        [".sh"] = Code,
        [".bat"] = Code,
        [".ps1"] = Code,
    };

    public static FileIconInfo Resolve(string name, bool isDirectory, bool isExpanded)
    {
        if (isDirectory)
        {
            return isExpanded ? FolderOpen : FolderClosed;
        }

        var extension = Path.GetExtension(name);
        return ByExtension.TryGetValue(extension, out var icon) ? icon : File;
    }

    private static FileIconInfo Create(string path, string color)
        => new(Geometry.Parse(path), new SolidColorBrush(Color.Parse(color)));
}
