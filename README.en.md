# Weftline

[中文](README.md) | **English**

Weftline is a story demo / narrative demonstration tool: write scripts, inspect the story graph, and export a playable HTML demo (planned for the C++ core).

Current stage: the C++ language core and desktop editor are usable. The desktop app now has an IDE-style shell: project explorer, multi-tab AvaloniaEdit editor, dual-lane syntax/project analysis, merged diagnostics, hover, completion, status bar, folder switching, and basic file operations. Single-file syntax diagnostics and project semantics are scheduled independently and applied by version; project compilation syncs only changed documents incrementally. The story graph supports layout sidecars, hover summaries, double-click navigation, chapter-edge details, badges, filters, and shortest-path highlighting; the Problems panel is also wired up. An in-app story preview supports step-by-step reading, choices, conditions/effects, images, and local start modules. HTML export will be implemented in C++ later.

## File types

| Extension | Role |
|---|---|
| `.weft` | Chapter script: chapter title + top-level declaration area (optional groups) + modules |
| `.wstory` | Project overview: project info + chapters + declarations generated from `.weft` + hand-written descriptions |

- One `.weft` file is one chapter.
- `.weft` is the only declaration source.
- `.wstory` is not a declaration entry point; declarations are generated, descriptions are hand-written and preserved.
- All `.weft` files in one flat folder belong to the same story.


## Desktop app

- Top menu bar, resizable project explorer, central editor area, and bottom status bar.
- Project explorer: directory/file tree with lazy loading and extension-based icons; supports new file, new folder, rename, delete, and switching the project folder from the menu.
- Editor: multi-file AvaloniaEdit tabs with close buttons, Weft syntax highlighting, line numbers, current-line highlight, a solid gutter separator, and modified markers; includes merged syntax/project diagnostics, squiggles, hover, and completion.
- Status bar: caret position, selection length, language, current module/scene, module/variable counts, diagnostics summary, and operation status.
- Story graph: live projection with chapter/module layers, entry/exit markers, dragging, zooming, and an END sink; hovering a module shows its summary, double-clicking a module or module edge jumps to source, and double-clicking a chapter edge shows underlying branches. Filters by character/scene/variable, badges for unreachable modules or modules that cannot reach END, and shortest-path highlighting for the selected module are also available. The Save Layout command persists manual layout and zoom to `weftline.layout.json` in the project root.
- Theme: SukiUI light theme; Inter for UI text and JetBrains Mono for the editor when available.
- The story graph and Problems panel are wired up; Output/Project Info panels are not yet.

### Build and run

Build the native core first, then the app:

```bash
cmake -S native -B native/out/build/release -G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF
cmake --build native/out/build/release
dotnet build Weftline.slnx
dotnet run --project src/Weftline.App
```

On Windows, run the same commands from the “x64 Native Tools Command Prompt for VS 2022”; `Weftline.App.csproj` copies `weftline_core.dll` to the output directory automatically. On Linux it copies `libweftline_core.so`.

### Tests and benchmarks

```bash
dotnet test Weftline.slnx
dotnet run --project bench/Weftline.Benchmarks -c Release
```

- `tests/Weftline.Editor.Tests` covers diagnostic merging, host integration, and syntax-analysis scheduling.
- `bench/Weftline.Benchmarks` covers 10KB / 100KB / 1MB single-file parsing, project compilation, and `TextDocument` snapshots.

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
