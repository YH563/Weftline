namespace Weftline.App.Models;

public enum ProjectFileChangeKind
{
    Created,
    Renamed,
    Deleted,
}

public sealed record ProjectFileChange(
    ProjectFileChangeKind Kind,
    string Path,
    string? OldPath = null);
