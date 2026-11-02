# Weftline Script Language Documentation

[中文](../README.md) | **English**

Weftline is a text format and tool for game scripts. It aims for three things:

1. Writing feels like writing a script, not like programming.
2. The story structure maps directly to a graph.
3. A playable HTML demo can be exported with one command.

Current stage: the language core is usable; the desktop editor, graph view, and HTML export are still in development.

---

## Positioning

Weftline is a **story demo / narrative demonstration tool**, not a game engine and not a commercial script production pipeline.

It is responsible for:

- quickly writing a playable story demo;
- visualizing story structure;
- recording and demonstrating story routes;
- exporting a zero-install HTML demo.

Non-goals: portraits, audio, saves, localization, voice, RPG stats/inventory, real-time engine logic, large-scale collaboration pipelines.

---

## File types

| Extension | Role |
|---|---|
| `.weft` | Module script: top-level symbol declarations + modules |
| `.wstory` | Project overview: project info + declarations generated from `.weft` + hand-written descriptions |

- `.weft` is the only declaration source.
- `.wstory` is not a declaration entry point; declarations are generated, descriptions are hand-written and preserved.
- All `.weft` files in one flat folder belong to the same story.

---

## Design principles

- **Prose first**: text lives in `[]` and is mostly plain text.
- **Modules are graph nodes**: a module is one linear segment ending at an exit.
- **Exits are the branching interface**: `@next` declares all exits.
- **Declarations at the top of `.weft`**: variables, characters, items, and scenes are grouped.
- **Explicit text references**: characters and items in text are marked with `{name}`.
- **State is optional**: a project without variables still works.
- **Indentation is display-only**: 4 spaces per level; the parser ignores it.
- **No multi-level conditions**: flat exit blocks + boolean expressions + module splitting.

---

## Minimal example

`.weft`:

```text
@chars
    @char 萧影

@scenes
    @scene 街道

@title 开场
    @scene 街道
    [雨水模糊了视线，{萧影}站在街口。]
    萧影:[你来了。]
@next -> END
```

`.wstory`:

```text
@project 雨夜
    @summary [一个关于信任的故事。]

    @chars
        @char 萧影 [主角。]

    @scenes
        @scene 街道 [雨夜的街道。]
```

---

## Documentation index

| Document | Contents |
|---|---|
| [language-spec.md](./language-spec.md) | Project structure, symbols, scenes, state, execution semantics |
| [grammar.md](./grammar.md) | Lexing, EBNF, parsing conventions |
| [examples.md](./examples.md) | Complete examples and walkthrough |
| [diagnostics.md](./diagnostics.md) | Diagnostic codes and common errors |

---

## Key conventions

1. **Text references**: `{}` only refers to characters or items, never variables.
2. **Module ID**: the `@title` text is the ID; no separate `@id` yet.
3. **Entry**: no entry declaration; the default is the first module.
4. **Scenes**: an optional module tag, not a graph node; chapters are deferred.
5. **Variables**: boolean only; declarations must provide an initial value; runtime changes only in branch effects.
6. **Images**: v0 supports `@fig "path"`, embedded on export.
