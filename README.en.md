# Weftline

[中文](README.md) | **English**

Weftline is a story demo / narrative demonstration tool: write scripts, inspect the story graph, and export a playable HTML demo.

Current stage: the language core is usable; the desktop editor, graph view, and HTML export are still in development.

## File types

| Extension | Role |
|---|---|
| `.weft` | Chapter script: chapter title + top-level symbol declarations + modules |
| `.wstory` | Project overview: project info + chapters + declarations generated from `.weft` + hand-written descriptions |

- One `.weft` file is one chapter.
- `.weft` is the only declaration source.
- `.wstory` is not a declaration entry point; declarations are generated, descriptions are hand-written and preserved.
- All `.weft` files in one flat folder belong to the same story.

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
