# Examples

[中文](../examples.md) | **English**

This document contains a minimal demo and a complete example with conditions, effects, and images.

## 1. Minimal demo (no globals)

```
@title opening
@char xiaoying

[
Rain blurred the view.
]

xiaoying:[You came.]

@next(xiaoying):[Continue] -> hospital
    ()[Turn back] -> leave

@title hospital
@char xiaoying

[
They walked into the hospital.
]

@next -> END

@title leave
@char xiaoying

[
Xiaoying turned and left.
]

@next -> END
```

Key points:

- No `@globals`, no state.
- Branching is expressed by target modules.
- Both terminal modules use `@next -> END`.
- The project starts at the first module, `opening`.

## 2. Complete example

```
@globals
@var trust_low false
@var has_key false

@title believe
@char xiaoying
@char shadow

[
Rain blurred the view.
]

xiaoying:[You came.]

shadow:[Do not look back.]

@fig "assets/hospital.png"

@set trust_low true

@next(xiaoying):(trust_low)[Believe him] -> hospital {
    @set has_key true
}
    ()[Stay silent] -> silence

@title hospital
@char xiaoying

[
They walked into the hospital.
]

@next():(has_key) -> basement
    () -> corridor

@title basement
@char xiaoying

[
Xiaoying found the key.
]

@next -> END

@title corridor
@char xiaoying

[
The corridor was empty.
]

@next -> END

@title silence
@char xiaoying

[
Xiaoying said nothing.
]

@next -> END
```

## 3. Graph

```
            +---------+
            | believe |
            +---------+
              |     |
   [Believe]  |     | [Stay silent]
              v     v
        +---------+ +---------+
        | hospital| | silence |
        +---------+ +---------+
           |   |
 (has_key) |   | ()
           v   v
    +---------+ +---------+
    | basement| | corridor|
    +---------+ +---------+
           \     /
            v   v
           +-----+
           | END |
           +-----+
```

- `believe` is choice mode: both branches have labels, and the second is the unconditional fallback.
- `hospital` is automatic mode: conditions are checked in order; `has_key` goes to `basement`, otherwise `corridor`.
- `END` is the only terminal.

## 4. Walkthrough

### Path A: choose "Believe him"

1. Enter `believe`.
2. Show narration, dialogue, and image.
3. Execute `@set trust_low true`.
4. Exit evaluation:
   - `(trust_low)[Believe him]` is true and visible.
   - `()[Stay silent]` is unconditional and visible.
5. The player selects "Believe him".
6. Execute `@set has_key true`.
7. Enter `hospital`.
8. `hospital` routes automatically:
   - `(has_key)` is true → enter `basement`.
9. `basement` → `END`.

### Path B: choose "Stay silent"

1. Enter `believe`.
2. `trust_low` becomes `true`.
3. The player selects "Stay silent".
4. Enter `silence`.
5. `silence` → `END`.

### Path C: `has_key` is false (debug override)

1. Start at `believe` and choose "Believe him".
2. In the preview debug panel, override `has_key` to `false`.
3. `hospital` sees `(has_key)` false and routes to `corridor`.
4. `corridor` → `END`.

## 5. Single-module preview

- Start at `believe`: the full example can be played because all target modules are loaded.
- Start at `hospital`: begins there, using `has_key = false` by default, so it goes to `corridor`.
- Export `believe` in isolation: external targets become demo ends or stubs.

## 6. Language features covered

- `@globals` and global boolean variables
- `@title` module boundaries
- `@char` character declarations
- Narration blocks `[]`
- Dialogue blocks `character:[]`
- `@fig` image insertion
- `@set` state updates
- Choice-mode exits (character, condition, label, fallback)
- Automatic-mode exits (condition + fallback)
- Branch effect blocks `{}`
- `@next -> END` termination
- Unconditional fallback branches
