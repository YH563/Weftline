# Weftline Language Specification

[中文](../language-spec.md) | **English**

This document defines the v0 semantics. See [grammar.md](./grammar.md) for the concrete syntax.

## 1. Overview

Weftline organizes a story as **modules**:

- A module is a linear narrative segment.
- The end of a module is an **exit block** that declares where the story can go next.
- Modules connect through exits to form the story graph.
- `END` is the single terminal module.
- Global state is optional and only introduced when conditions or persistence are needed.

Graph model:

```
Module = node
Exit   = edge
END    = unique sink
```

## 2. Files, modules, and globals

### 2.1 Files

- A file can contain any number of modules.
- Before the first `@title`, only `@globals` and comments are allowed.

### 2.2 Modules

```
@title believe
@char xiaoying
@char shadow

[
Text...
]

@next(xiaoying):[Believe him] -> hospital
    ()[Stay silent] -> silence
```

- `@title` starts a module.
- **The module ID is the `@title` text**, trimmed.
- Module IDs must be unique across the project.
- IDs may contain spaces but not `{` or `}`.
- A new `@title` ends the previous module.

### 2.3 Globals

- `@globals` is the **single** global variable declaration block in the project.
- It may appear:
  - at the top of a story file, before the first `@title`; or
  - in a dedicated globals file (detected by content: it contains `@globals` and no `@title`).
- `@var` is only allowed inside `@globals`.

```
@globals
@var trust_low false
@var has_key false
```

Rules:

- Variables are boolean only.
- At most one `@globals` block per project.
- A variable is declared exactly once.
- Without `@globals`, the variable table is empty; modules without state still work.

### 2.4 Characters

- `@char` declares a character used by the current module.
- Character scope is the module.
- Dialogue speakers and the character in `@next(character)` must be declared in the current module.
- Different modules may declare the same character independently.

## 3. Content blocks

Content blocks execute in source order.

### 3.1 Narration

```
[
Rain blurred the view.
]
```

- The block is plain text; no keywords are parsed inside.
- Inline form: `[Rain blurred the view.]`

### 3.2 Dialogue

```
xiaoying:[
You came.
I waited.
]
```

- Inline form: `xiaoying:[You came.]`
- The speaker must be declared via `@char` in the current module.
- The block is still plain text.
- Always write the speaker, even in a single-character scene, so voice, portraits, localization, and search have structured data.

### 3.3 Figures

```
@fig "assets/hospital.png"
```

- Inserts an image at the current content position.
- The path is a quoted string relative to project assets.
- Supported formats: PNG, JPEG, WebP, GIF, SVG.
- A figure is node content, not a graph node.
- v0 embeds images as base64 in exports.

### 3.4 State updates

```
@set trust_low true
```

- Updates a global boolean variable.
- The variable must be declared in `@globals`.
- Executes immediately in source order.

### 3.5 Comments

```
// comment
```

- Only at the structural level.
- Inside text blocks, `//` is plain text.

## 4. Exit blocks

The exit block is the module's **branch interface** and its end boundary.

### 4.1 Full form

```
@next(character):(condition)[label] -> target {
    @set has_key true
}
    (condition)[label] -> target
    ()[label] -> target
```

### 4.2 Shorthand

```
@next -> target
```

A single unconditional automatic branch, equivalent to:

```
@next(): -> target
```

### 4.3 Modes

The mode is determined by whether the `@next` header specifies a character. Modes cannot be mixed inside one block.

| Header | Mode | Requirement |
|---|---|---|
| `@next(character):` | Choice | Every branch must have `[label]`. |
| `@next():` | Automatic | Branches must not have `[label]`. |
| `@next -> target` | Automatic (single branch) | No label, no condition. |

### 4.4 Choice mode

- Branches whose condition is true are shown to the player.
- Branches whose condition is false are hidden and cannot be selected.
- At least one unconditional branch is required as a fallback, so at least one option is always visible.

### 4.5 Automatic mode

- Branches are checked in source order; the first true branch is taken.
- False branches are skipped.
- At least one unconditional branch is required as a fallback, so routing never dead-ends.
- Conditions need not be mutually exclusive; order defines priority.

### 4.6 Conditions

Conditions are boolean expressions in parentheses:

```
(trust_low)
(a and not b)
((a or b) and c)
```

- Variables come from the global variable table.
- Operators: `and`, `or`, `not`.
- Parentheses group.
- Precedence: `not` > `and` > `or`.
- `()` means unconditional.
- No comparisons, arithmetic, or function calls.

### 4.7 Branch effects

```
-> hospital {
    @set has_key true
    @set trust_low false
}
```

Inline form:

```
-> hospital { @set has_key true; @set trust_low false }
```

- Only `@set` and comments are allowed inside the block.
- Effects run after the branch is selected and before the target module is entered.
- Conditions are evaluated against the state **before** effects run.
- Duplicate assignments in one effect block overwrite in order and produce a warning.

### 4.8 Exit constraints

- Every module **must** have an exit block.
- The exit block must be the last part of the module.
- No narration, dialogue, `@fig`, or `@set` may follow it.
- Terminal modules use `@next -> END`.
- Multi-level conditions are not supported; use boolean expressions, ordered routing, or more modules.

## 5. State and execution

- Global boolean variables only.
- A single declaration point: `@globals`.
- Without `@globals`, state is empty.
- Single cursor: exactly one current module at a time.
- Content blocks execute in order.
- At the exit block:
  - Choice mode waits for the player.
  - Automatic mode takes the first true branch.
- After a branch is selected, run its effects, then enter the target module.
- Last write wins.
- Given a path and player choices, execution is deterministic.

## 6. Entry and single-module processing

The language has **no entry declaration**.

### 6.1 Project preview

- Default entry: the **first module** in project order.
- Project order = file order + module order within each file.
- The UI can start from any module.

### 6.2 Single-module processing

| Mode | Behavior |
|---|---|
| Isolated compile | Load globals (if any) + the current module; unresolved external targets produce warnings, not blockers. |
| Preview from module | Load the whole project, start at the selected module, and follow exits. |
| Export module | Export only the selected module; external exits become demo ends or stubs. |

### 6.3 External variables

- All variable references resolve against the unique `@globals` table.
- Missing variables are errors with a quick fix to add them.
- In isolated mode the rule is the same; without a globals block the table is empty.

### 6.4 END

- `END` is a reserved module ID, built into the compiler.
- It is the only terminal; it cannot be defined and has no exits.
- Terminal modules explicitly point to it: `@next -> END`.

## 7. Graph mapping

| Source | Graph |
|---|---|
| `@title` | Node (ID = title) |
| Content blocks | Node content |
| Each `@next` branch | Outgoing edge |
| Block character | Edge attribute |
| Condition | Edge condition |
| Label | Edge label (choice mode) |
| `@set` inside `{}` | Edge effects |
| `END` | Unique sink |

The entry is not part of the graph; it is the start selected for preview or export.

## 8. Non-goals and v0 exclusions

### 8.1 Non-goals

- Visual-novel production: sprites, expressions, BGM, SFX, save/load, backlog, localization, voice.
- RPG quests, stats, inventory.
- Real-time engine triggers, cutscenes, QTE.
- Large production pipelines and multi-user collaboration.

### 8.2 Deferred from v0

- Chapters (`@chapter` tags may be added later).
- Numeric state and comparisons.
- Multi-level / nested conditions.
- Multiple characters per exit block; per-branch characters.
- Multiple entries.
- Event contracts and data-flow analysis.
- True concurrency, schedulers, vertical alignment.
- Engine export (Unity / Godot).

## 9. Review points

1. Mode rule: character present = choice mode; no character = automatic mode.
2. `@title` as module ID.
3. Terminal modules use `@next -> END`.
4. Globals are declared only in the single `@globals` block.
5. Chapters are not modeled in v0.
