# Weftline Script Language Documentation

[中文](../README.md) | **English**

Weftline is a text format and tool for game scripts. It aims for three things:

1. Writing feels like writing a script, not like programming.
2. The story structure maps directly to a graph.
3. A playable HTML demo will be exportable with one command (planned for the C++ core).

Current stage: the C++ language core and desktop editor are usable. The main window includes single-file syntax diagnostics, project semantic analysis, merged diagnostics, a live story graph with layout sidecars, filters and path highlighting, hover, and completion. An in-app story preview supports step-by-step reading, choices, images, and local start modules; HTML export will be implemented in C++ later.

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
| `.weft` | Chapter script: chapter title + top-level declaration area (optional groups) + modules |
| `.wstory` | Project overview: project info + chapters + declarations generated from `.weft` + hand-written descriptions |

- One `.weft` file is one chapter.
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
@title 第一章 街道

@char 萧影

@scene 街道

@module 开场
    @scene 街道
    [雨水模糊了视线，{萧影}站在街口。]
    萧影:[你来了。]
@next -> END
```

`.wstory`:

```text
@project 雨夜
    @summary [一个关于信任的故事。]

    @chapters
        @chapter 第一章 街道 [雨夜的开端。]

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
2. **Chapters**: the `@title` on the first line of a `.weft` file is the chapter title; chapters are groups, not graph nodes.
3. **Module ID**: `@module` is the module ID; IDs are unique across the project.
4. **Entry**: no entry declaration; the default is the first module of the first file, while other modules with no incoming edges act as candidate entries for reachability checks and graph badges.
5. **Scenes**: a switch marker inside the content stream, not a graph node, and it does not split modules.
6. **Variables**: boolean only; declarations must provide an initial value; runtime changes only in branch effects.
7. **Images**: v0 supports `@fig "path"`; it will be embedded once HTML export is wired up.
