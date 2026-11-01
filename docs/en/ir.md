# StoryGraph IR

[中文](../ir.md) | **English**

StoryGraph is the intermediate representation shared by the compiler, graph view, and HTML exporter. It is a pure projection of the language: it contains no formatting, comments, or indentation.

## 1. Top level

```json
{
  "schemaVersion": 1,
  "end": "END",
  "entry": "trust",
  "globals": {
    "trust_low": false,
    "has_key": false
  },
  "modules": []
}
```

| Field | Type | Description |
|---|---|---|
| `schemaVersion` | number | Currently `1`. |
| `end` | string | Always `"END"`. |
| `entry` | string | Entry module selected for this compile/export. |
| `globals` | object | Boolean defaults keyed by variable name. |
| `modules` | Module[] | Modules in source order. |

`entry` is not a language construct. It is the start selected for a preview or export; project default is the first module.

## 2. Module

```json
{
  "id": "trust",
  "title": "trust",
  "chapter": null,
  "characters": ["xiaoying", "shadow"],
  "content": [],
  "exits": []
}
```

| Field | Type | Description |
|---|---|---|
| `id` | string | Module ID; equals `title` in v0. |
| `title` | string | Display title. |
| `chapter` | string \| null | Always null in v0; reserved for a future `@chapter`. |
| `characters` | string[] | Characters declared in this module. |
| `content` | ContentBlock[] | Ordered content blocks. |
| `exits` | Exit[] | Ordered exit branches. |

## 3. ContentBlock

Content blocks are tagged by `kind`.

### 3.1 Narration

```json
{ "kind": "narration", "text": "Rain blurred the view." }
```

### 3.2 Dialogue

```json
{ "kind": "dialogue", "speaker": "xiaoying", "text": "You came." }
```

### 3.3 Figure

```json
{ "kind": "figure", "path": "assets/hospital.png" }
```

### 3.4 State update

```json
{ "kind": "set", "name": "trust_low", "value": true }
```

## 4. Exit

```json
{
  "character": "xiaoying",
  "condition": { "kind": "ref", "name": "trust_low" },
  "label": "Believe him",
  "target": "hospital",
  "effects": [
    { "name": "has_key", "value": true }
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `character` | string \| null | Block-level character; null means automatic mode. |
| `condition` | BoolExpr \| null | Condition; null means unconditional. |
| `label` | string \| null | Choice label; null in automatic mode. |
| `target` | string | Target module ID or `"END"`. |
| `effects` | Assignment[] | Assignments executed after selection. |

Exit order matters: automatic mode takes the first true branch in array order.

## 5. BoolExpr

```json
{ "kind": "literal", "value": true }
{ "kind": "ref", "name": "trust_low" }
{ "kind": "not", "expr": { "kind": "ref", "name": "a" } }
{ "kind": "and", "left": {}, "right": {} }
{ "kind": "or", "left": {}, "right": {} }
```

Only boolean literals, variable references, `not`, `and`, and `or` are supported. There are no comparisons or arithmetic.

## 6. Assignment

```json
{ "name": "has_key", "value": true }
```

## 7. Example (excerpt)

```json
{
  "schemaVersion": 1,
  "end": "END",
  "entry": "believe",
  "globals": {
    "trust_low": false,
    "has_key": false
  },
  "modules": [
    {
      "id": "believe",
      "title": "believe",
      "chapter": null,
      "characters": ["xiaoying", "shadow"],
      "content": [
        { "kind": "narration", "text": "Rain blurred the view." },
        { "kind": "dialogue", "speaker": "xiaoying", "text": "You came." },
        { "kind": "figure", "path": "assets/hospital.png" },
        { "kind": "set", "name": "trust_low", "value": true }
      ],
      "exits": [
        {
          "character": "xiaoying",
          "condition": { "kind": "ref", "name": "trust_low" },
          "label": "Believe him",
          "target": "hospital",
          "effects": [{ "name": "has_key", "value": true }]
        },
        {
          "character": "xiaoying",
          "condition": null,
          "label": "Stay silent",
          "target": "silence",
          "effects": []
        }
      ]
    }
  ]
}
```

## 8. Layout sidecar

Layout coordinates are not part of StoryGraph. They are stored separately so text or semantic changes do not discard manual layout.

```json
{
  "schemaVersion": 1,
  "positions": {
    "believe": { "x": 0, "y": 0 },
    "hospital": { "x": 240, "y": 0 }
  }
}
```

- Keys are module IDs.
- Written after the first automatic layout; user drags override individual nodes.
- "Relayout" only resets nodes without manual positions.

## 9. Export notes

- HTML export consumes StoryGraph directly.
- Images are resolved by `path` and embedded as base64.
- The player implements only: text advance, choices, conditions, boolean state, images, and ending.
- When exporting a single module, external targets become demo ends or stubs.
