# Weftline

[中文](README.md) | **English**

A prose-first narrative demo tool: write scripts, inspect the story graph, and export a playable HTML demo.

Current status: **the parser front-end is implemented** — it parses globals, modules, content blocks, and exit branches (with conditions and effects) and reports diagnostics. `StoryGraph` IR is not connected yet.

## Tech stack

- .NET 10 / C#
- Avalonia (cross-platform desktop UI)
- xUnit (tests)

## Solution layout

```
Weftline.slnx
src/
  Weftline.Language/        Language core: parsing, diagnostics, StoryGraph IR (no UI dependency)
  Weftline.Export/          StoryGraph -> single-file HTML / embedded assets
  Weftline.App/             Avalonia UI: editor, graph view, asset panel
  Weftline.Cli/             CLI parser for manual testing and learning
tests/
  Weftline.Language.Tests/  Unit tests for the language core
docs/                       Language and architecture documentation
samples/                    Runnable sample scripts
```

Dependency direction:

```
Weftline.App ──> Weftline.Export ──> Weftline.Language
Weftline.Language.Tests ──> Weftline.Language
```

`Weftline.Language` must not reference the UI or export layers. This boundary keeps the core testable and portable.

## Build and test

```bash
dotnet build
dotnet test
```

## Runtime and compatibility

- Target framework: `net10.0` (.NET 10 LTS, supported until November 2028).
- .NET 8 is not the primary target: its LTS support ends in November 2026.
- `global.json` pins the .NET 10 feature band; `rollForward: latestFeature` allows patch SDKs in the same band.
- Release builds use self-contained publishing so users do not need to install the runtime:

```bash
dotnet publish src/Weftline.App -c Release -r win-x64 --self-contained true
dotnet publish src/Weftline.App -c Release -r linux-x64 --self-contained true
dotnet publish src/Weftline.App -c Release -r osx-arm64 --self-contained true
```

- If `Weftline.Language` later needs to run inside an engine (Unity / Godot), consider multi-targeting `net8.0` / `netstandard2.1`. This is intentionally deferred for now.

## Documentation

- [docs/en/README.md](docs/en/README.md) — documentation index and positioning
- [docs/en/architecture.md](docs/en/architecture.md) — layers, responsibilities, data flow
- [docs/en/grammar.md](docs/en/grammar.md) — lexing and EBNF
- [docs/en/language-spec.md](docs/en/language-spec.md) — language semantics
- [docs/en/ir.md](docs/en/ir.md) — StoryGraph IR
- [docs/en/diagnostics.md](docs/en/diagnostics.md) — diagnostic codes
- [docs/en/examples.md](docs/en/examples.md) — examples and walkthrough
- [docs/en/compiler-walkthrough.md](docs/en/compiler-walkthrough.md) — parser front-end walkthrough

Chinese documentation is available under [docs/README.md](docs/README.md).

## Milestones

1. **M1** Language core: lexer / parser / IR / diagnostics
2. **M2** Graph view: modules, exits, `END`, entry points
3. **M3** HTML export and player
4. **M4** Editor integration: auto-indent, completion, error locations
5. **M5** Image import and polish
