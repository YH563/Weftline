# Weftline Documentation

[中文](../README.md) | **English**

**Status: v0 draft for review**

Weftline is a text format and tool for game scripts. It aims at three things:

1. Writing feels like writing a script, not writing a program.
2. The story structure maps directly to a graph.
3. A playable HTML demo can be exported with one action.

## Positioning

Weftline is a **narrative demo tool**, not a game engine or a commercial narrative production pipeline.

It is responsible for:

- writing playable story demos quickly;
- visualizing the story structure;
- exporting a self-contained HTML demo that can be shared with anyone.

It is not responsible for:

- visual-novel production features (sprites, BGM, save/load, backlog, localization, voice);
- RPG quests, stats, inventory;
- real-time triggers, cutscenes, QTE inside an engine;
- large-scale production pipelines or team collaboration.

Those belong to downstream engines or dedicated tools.

## Document index

| Document | Contents |
|---|---|
| [architecture.md](./architecture.md) | Layers, responsibilities, data flow, editing model |
| [grammar.md](./grammar.md) | Lexing, EBNF, parsing rules |
| [language-spec.md](./language-spec.md) | Modules, globals, content, exits, state, execution |
| [ir.md](./ir.md) | StoryGraph IR and graph export format |
| [diagnostics.md](./diagnostics.md) | Diagnostic codes and quick fixes |
| [examples.md](./examples.md) | Complete examples and walkthrough |
| [compiler-walkthrough.md](./compiler-walkthrough.md) | Parser front-end walkthrough |

Chinese versions live under [docs/](../README.md).

## Design principles

- **Prose first**: text lives inside `[]`; the block is opaque and is not scanned for keywords.
- **A module is a graph node**: one module is a linear story segment ending at an exit block.
- **The exit is the branch interface**: choices and conditions live only there.
- **State is optional**: stories without `@globals` still parse, preview, and export.
- **All keywords use `@`**: only `@`-prefixed names are directives.
- **No multi-level conditions**: flat exits + boolean expressions + more modules.
- **Single-cursor runtime**: no concurrency, no scheduler.

## Minimal example

```
@title opening
@char xiaoying

[
Rain blurred the view.
]

xiaoying:[You came.]

@next -> END
```

No globals, no branches, and it is still a complete demo.

## Review points

1. **Mode rule**: `@next(character):` means choice mode; `@next():` means automatic mode.
2. **Module ID**: the `@title` text is the module ID; no separate `@id` yet.
3. **Entry**: there is no entry declaration; the project starts at the first module, and a single-module demo starts at that module.
4. **Chapters**: not modeled in v0; a future `@chapter` tag may group modules.
5. **Images**: `@fig "path"` is supported and embedded in exports.
