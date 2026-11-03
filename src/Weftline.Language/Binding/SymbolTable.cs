using Weftline.Language.Source;

namespace Weftline.Language.Binding;

/// <summary>
/// 项目级符号种类。
/// Project-level symbol kinds.
/// </summary>
public enum SymbolKind
{
    Variable,
    Character,
    Item,
    Scene,
}

/// <summary>
/// 一个项目级符号：名字、种类、定义位置，变量额外带有初始值。
/// A project-level symbol: name, kind, definition location; variables also carry
/// their initial value.
/// </summary>
public sealed record SymbolEntry(
    string Name,
    SymbolKind Kind,
    SourceLocation Location,
    bool? Value = null);

internal sealed class SymbolTable
{
    public Dictionary<string, SymbolEntry> Symbols { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, bool> Variables { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, SourceLocation> Characters { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, SourceLocation> Items { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, SourceLocation> Scenes { get; } = new(StringComparer.Ordinal);

    public bool TryDeclare(SymbolEntry entry, out SymbolEntry? existing)
    {
        if (Symbols.TryGetValue(entry.Name, out existing))
        {
            return false;
        }

        Symbols[entry.Name] = entry;

        switch (entry.Kind)
        {
            case SymbolKind.Variable:
                Variables[entry.Name] = entry.Value ?? false;
                break;

            case SymbolKind.Character:
                Characters[entry.Name] = entry.Location;
                break;

            case SymbolKind.Item:
                Items[entry.Name] = entry.Location;
                break;

            case SymbolKind.Scene:
                Scenes[entry.Name] = entry.Location;
                break;
        }

        return true;
    }
}
