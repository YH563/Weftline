# Weftline

[中文](README.md) | **English**

Weftline is a story demo / narrative demonstration tool: write scripts, inspect the story graph, and export a playable HTML demo (planned for the C++ core).

Current stage: the C++ language core and desktop editor are usable. The desktop app now has an IDE-style shell: project explorer, multi-tab AvaloniaEdit editor, status bar, folder switching, and basic file operations. The story graph UI and Problems panel are wired up; HTML export will be implemented in C++ later.

## File types

| Extension | Role |
|---|---|
| `.weft` | Chapter script: chapter title + top-level symbol declarations + modules |
| `.wstory` | Project overview: project info + chapters + declarations generated from `.weft` + hand-written descriptions |

- One `.weft` file is one chapter.
- `.weft` is the only declaration source.
- `.wstory` is not a declaration entry point; declarations are generated, descriptions are hand-written and preserved.
- All `.weft` files in one flat folder belong to the same story.


## Desktop app

- Top menu bar, resizable project explorer, central editor area, and bottom status bar.
- Project explorer: directory/file tree with lazy loading and extension-based icons; supports new file, new folder, rename, delete, and switching the project folder from the menu.
- Editor: multi-file AvaloniaEdit tabs with close buttons, Weft syntax highlighting, line numbers, current-line highlight, a solid gutter separator, and modified markers.
- Status bar: caret position, selection length, language, current module/scene, module/variable counts, diagnostics summary, and operation status.
- Theme: SukiUI light theme; Inter for UI text and JetBrains Mono for the editor when available.
- The story graph and Problems panel are wired up; Output/Project Info panels are not yet.

```bash
dotnet run --project src/Weftline.App
```

### Main UI packages

| Package | Purpose |
|---|---|
| `Avalonia` / `Avalonia.Desktop` | Desktop UI and windows |
| `SukiUI` | Theme, window chrome, notifications, popups |
| `Avalonia.AvaloniaEdit` | Code editor |
| `CommunityToolkit.Mvvm` | ObservableObject / RelayCommand |
| `Avalonia.Fonts.Inter` | UI font |

## Documentation

- [docs/en/README.md](docs/en/README.md) — documentation index and positioning
- [docs/en/language-spec.md](docs/en/language-spec.md) — language semantics
- [docs/en/grammar.md](docs/en/grammar.md) — lexing and EBNF
- [docs/en/examples.md](docs/en/examples.md) — examples and walkthrough
- [docs/en/diagnostics.md](docs/en/diagnostics.md) — diagnostic codes and common errors

## Examples

- [samples/believe.weft](samples/believe.weft)
- [samples/believe.wstory](samples/believe.wstory)

## Positioning

Weftline is a **story demo / narrative demonstration tool**, not a game engine and not a commercial script production pipeline. Portraits, audio, saves, localization, voice, RPG stats/inventory, and real-time engine logic are out of scope.
