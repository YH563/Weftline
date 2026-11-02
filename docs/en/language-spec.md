# Weftline Language Specification

[中文](../language-spec.md) | **English**

This document defines v0 semantics. See [grammar.md](./grammar.md) for syntax.

---

## 1. Overview

Weftline organizes a script into **modules**:

- A module is one **linear story segment**.
- A module ends with an **exit block** that declares where it can go.
- Modules connect through exits to form the story graph.
- `END` is the only terminal module.
- Global state is optional boolean variables.

Graph model:

```text
Module = node
Exit   = edge
END    = the only sink
```

---

## 2. Project and files

### 2.1 Project

A flat folder is one story:

```text
my-story/
  project.wstory     project overview: project info + generated declarations + hand-written descriptions
  chapter1.weft      module script: top-level declarations + modules
  chapter2.weft
  assets/            image assets
  .weftline/         index cache, recommended gitignore
```

- All `.weft` files in the folder belong to one story.
- No subdirectories and no cross-folder imports.
- `.weft` is the only declaration source.
- `.wstory` is a generated overview; declarations are generated, descriptions are hand-written and preserved.

### 2.2 `.weft` top-level declarations

```text
@vars
    @var 信任 false
    @var 有钥匙 false

@chars
    @char 萧影
    @char 影子

@items
    @item 医院钥匙

@scenes
    @scene 医院
```

Rules:

- All symbol declarations appear in top-level groups before modules.
- Groups are `@vars` / `@chars` / `@items` / `@scenes`.
- Variables must provide an initial value; `@var 信任` without a value is an error.
- Declarations do not carry descriptions.
- Symbol names are unique across the project, including across kinds.
- Missing symbols are errors; the tool does not auto-create declarations.

### 2.3 `.wstory` project overview

```text
@project 雨夜
    @summary [一个关于信任的故事。]

    @docs
        @doc 世界规则 [医院在雨夜不关门。]

    @chars
        @char 萧影 [主角。]
        @char 影子 [萧影的旧识。]

    @items
        @item 医院钥匙 [医院地下室的钥匙。]

    @scenes
        @scene 医院 [雨夜里的医院。]

    @vars
        @var 信任 false [萧影是否信任影子。]
        @var 有钥匙 false [是否拿到了医院钥匙。]
```

Rules:

- `@project` is top-level; groups are indented one level; entries one more (display only).
- `@summary` is a single line.
- Declarations (names and variable initial values) are generated from `.weft`.
- Descriptions are hand-written and preserved by the generator.
- `.wstory` must not contain `@set` or modules.
- Hand-editing declarations is overwritten by the next generation; descriptions are not.

---

## 3. Symbols

### 3.1 Characters, items, and scenes

- Characters are used by dialogue speakers and `@next(character)`.
- Items are metadata; text can reference them with `{item}`.
- A scene is an optional module grouping tag and does not create a graph node.
- A module has at most one `@scene`.
- A scene can span multiple modules; a module cannot change scenes internally.

### 3.2 Variables

- Boolean only.
- Declarations must provide an initial value.
- Runtime changes only happen in exit branch effects.
- Text content does not support `@set`.
- There are no entry overrides.

| Layer | Location | When it applies |
|---|---|---|
| Initial value | `.weft` top-level `@var` | Story start |
| Runtime change | Exit branch effect | After the branch is selected |

---

## 4. Content blocks

Content blocks execute linearly in source order.

### 4.1 Narration

```text
[雨水模糊了视线。]
```

Multi-line, with brackets attached to text:

```text
[雨水模糊了视线，
他抬起头。]
```

### 4.2 Dialogue

```text
萧影:[你来了。]
```

Multi-line:

```text
萧影:[你来了。
我等了很久。]
```

- The speaker must be a declared global character.

### 4.3 Text references

```text
[{萧影}推开门，手里攥着{医院钥匙}。]
```

- `{name}` only references declared characters or items.
- Variables cannot be referenced.
- Export shows the name only.
- The editor can jump from a reference to the description in `.wstory`.
- `{{` and `}}` are literal escapes.
- `{}` does not span lines and does not nest.

### 4.4 Images

```text
@fig "assets/hospital.png"
```

- Inserts an image at the current content position.
- The path is relative to project assets and must be double-quoted.

### 4.5 Comments

```text
// this is a comment
```

- Only effective at the structural level.
- Inside text blocks, `//` is plain text.

---

## 5. Exit blocks

### 5.1 Full form

```text
@next(character):(condition)[label] -> target {
    @set name true
}
    (condition)[label] -> target
    ()[label] -> target
```

### 5.2 Shorthand

```text
@next -> target
@next -> target { @set name true }
```

### 5.3 Modes

| Header | Mode | Branch requirement |
|---|---|---|
| `@next(character):` | choice | every branch must have a non-empty `[label]` |
| `@next():` | automatic | branches must not have `[label]` |
| `@next -> target` | automatic (single) | no label, no condition |

### 5.4 Choice mode

- All branches whose condition is true are shown.
- False branches are hidden.
- The player chooses one visible branch.
- At least one unconditional fallback branch is required.

### 5.5 Automatic mode

- Evaluate conditions in source order and take the first true branch.
- At least one unconditional fallback branch is required.
- Conditions need not be mutually exclusive; order defines priority.

### 5.6 Conditions

```text
(trust_low)
(a and not b)
((a or b) and c)
```

- Variables come from the global variable table.
- Operators: `and`, `or`, `not`.
- Precedence: `not` > `and` > `or`.
- No comparisons, arithmetic, or functions.

### 5.7 Branch effects

```text
-> 医院 {
    @set has_key true
    @set trust_low false
}
```

Or inline:

```text
-> 医院 { @set has_key true; @set trust_low false }
```

- Effect blocks only allow `@set` and comments.
- Order: evaluate condition → select branch → apply `@set` → enter target module.
- Conditions use the state **before** effects.
- Reassigning the same variable in one block: later wins, with a warning.

### 5.8 Exit constraints

- Every module must have an exit block.
- The exit block must be the last part of the module.
- Terminal modules use `@next -> END`.
- No multi-level conditions; express complexity with booleans, ordered routing, or module splitting.

---

## 6. Execution model

- Single cursor: exactly one current module at a time.
- Content blocks execute in order.
- At an exit block:
  - choice mode: wait for the player;
  - automatic mode: take the first true branch in order.
- After a branch is selected, apply its effects, then enter the target module.
- Given a path and choices, execution is deterministic.

---

## 7. Entry and single-module handling

The language has **no** entry declaration.

- Default entry = the first module in the project.
- Project order = file order + module order inside each file.
- The tool UI can start playback from any module.
- Full export uses the initial values from `.weft` `@var`.
- In single-module export, external targets become end or stubs.

### END

- `END` is a reserved module ID provided by the compiler.
- It is the only terminal point; it cannot be defined and has no exits.
- Terminal modules must explicitly point to it: `@next -> END`.

---

## 8. Graph mapping

| Text | Graph |
|---|---|
| `@title` | node (ID = title) |
| `@scene` | node grouping tag |
| content blocks | node content |
| each `@next` branch | one outgoing edge |
| `(character)` | edge attribute (block-level) |
| `(condition)` | edge condition |
| `[label]` | edge label (choice mode) |
| `@set` in `{}` | edge effect |
| `END` | the only sink |

---

## 9. Non-goals and deferred features

### 9.1 Non-goals

- Visual novel production: portraits, expressions, BGM, sound, saves, backlog, localization, voice.
- RPG quests, stats, inventory.
- Real-time triggers, cutscenes, QTE inside an engine.
- Large-project pipelines and multi-user collaboration.

### 9.2 Deferred in v0

- Chapters (possibly `@chapter` later)
- Numeric state and comparisons
- Multi-level / nested conditions
- Multiple entry declarations
- Event contracts, data-flow analysis
- True concurrency, schedulers
- Engine export (Unity / Godot)

---

## 10. Review points

1. Text `{}` references characters/items only, never variables.
2. `.weft` is the only declaration source; `.wstory` is a generated overview.
3. Variable declarations must include an initial value; runtime writes happen only in branch effects.
4. Scenes are only module tags, never graph nodes.
5. Indentation is display-only and does not affect parsing.
