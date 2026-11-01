# Architecture

[中文](../architecture.md) | **English**

This document describes the project structure and module responsibilities. Semantics are in [language-spec.md](./language-spec.md).

## 1. Positioning

Weftline is a **narrative demo tool**, not a game engine or a commercial narrative production pipeline.

It is responsible for:

- writing playable story demos quickly;
- visualizing the story structure;
- exporting a self-contained HTML demo.

Production features (sprites, audio, save/load, localization, voice, engine triggers) are out of scope.

## 2. Layers

```
                ┌──────────────────────┐
                │      Weftline.App    │  Avalonia UI
                │ editor / graph / panels │
                └──────────┬───────────┘
                           │
            ┌──────────────┴──────────────┐
            │                             │
   ┌────────▼────────┐          ┌─────────▼──────────┐
   │ Weftline.Export │          │ Weftline.Language  │
   │ HTML / SVG      │─────────>│ parse / diag / IR  │
   └─────────────────┘          └────────────────────┘
```

Dependency rules:

- `Weftline.Language` does not reference UI or export code.
- `Weftline.Export` depends only on `Weftline.Language`.
- `Weftline.App` may depend on both.
- Test projects depend only on the code under test.

## 3. Project responsibilities

### 3.1 Weftline.Language

Pure C# class library with no UI dependency.

Planned internal layout:

```
Source/
  SourceText.cs          text, line index, offsets
  TextSpan.cs            source ranges
Syntax/
  Parser.cs              line-oriented recursive descent
  ConditionParser.cs     boolean expression parser
  SyntaxFacts.cs         identifier and literal helpers
  Ast.cs                 modules, globals, content, exits
Binding/
  Binder.cs              module/variable/character resolution
  Validator.cs           graph and semantic checks
  Symbols.cs
Ir/
  StoryGraph.cs          nodes, exits, globals, entry
Diagnostics/
  Diagnostic.cs
  DiagnosticBag.cs
```

Responsibilities:

- Parse `.weft` source into an AST and StoryGraph.
- Produce structured diagnostics with source spans.
- Support forward references through two-phase parsing.
- Support isolated single-module compilation.
- No UI, layout, or HTML.

### 3.2 Weftline.Export

- Consumes `StoryGraph` and produces a single-file HTML demo.
- Embeds image assets.
- Ships a small vanilla JS player: text advance, choices, conditions, boolean state, ending.
- Does not depend on Avalonia.

### 3.3 Weftline.App

Avalonia desktop application.

Planned layout:

```
Views/          main window, editor, graph view
ViewModels/     main view, document, state panel
Controls/       custom-drawn graph view
Services/       files, autosave, asset import, layout sidecar
```

Responsibilities:

- Text editor (AvaloniaEdit) beside the graph view.
- Auto-indent exit branches; complete module titles, characters, variables.
- Preview and export entry points.
- Image import and asset panel.
- Persist layout coordinates in a sidecar file.

### 3.4 Weftline.Language.Tests

- Unit tests for parsing and diagnostics.
- Inline source strings; no file-system dependency.
- Later: IR snapshot tests and export smoke tests.

### 3.5 Weftline.Cli

- `parse` command that prints the AST and diagnostics.
- Intended for manual testing, learning, and debugging.
- No Avalonia dependency.

## 4. Data flow

```
.weft source
   │
   ▼
Parser ──> AST
   │
   ▼
Binder / Validator ──> StoryGraph IR + Diagnostics
   │
   ├──> Weftline.App    graph view / preview
   └──> Weftline.Export single-file HTML
```

Key principles:

- Source text is the single source of truth.
- The graph is a projection of the text; the tool does not regenerate the whole source.
- Layout coordinates are stored separately and never enter the language or IR.
- Every diagnostic carries a source span.

## 5. Editing model (graph -> source write-back)

### 5.1 Principles

- Source text is the document; AST nodes carry `TextSpan`.
- v0 does not introduce a CST. Manual graph connections do not need decompilation.
- GUI operations are predefined commands that compute minimal `TextEdit` lists.
- Text edits and GUI edits are serialized through one `SourceText`, then re-parsed.
- Undo/redo is based on the text edit stack.

### 5.2 Pipeline

```
GUI command
  -> IEditCommand.Compute(...)
  -> TextEdit[] (span + replacement)
  -> apply to SourceText
  -> re-parse
  -> refresh StoryGraph / diagnostics / graph view
```

### 5.3 Required anchors

```
Module.Span
Module.TitleSpan
ExitBlock.Span
ExitBlock.InsertionPoint
ExitBranch.Span
ExitBranch.TargetSpan
ExitBranch.ConditionSpan
ExitBranch.LabelSpan
ExitBranch.EffectSpan
ContentBlock.Span
SourceText line index
```

### 5.4 Commands and text operations

| Command | Text operation |
|---|---|
| AddEdge | Insert a branch line at the exit insertion point |
| RemoveEdge | Delete the branch line including its newline |
| RetargetEdge | Replace `TargetSpan` |
| EditCondition | Replace or insert `ConditionSpan` |
| EditLabel | Replace `LabelSpan` |
| AddEffect / RemoveEffect | Modify `@set` statements inside `{}` |
| AddModule | Insert a module template at the end or after the selected module |
| DeleteModule | Delete the module's `Span` |
| RenameModule | Replace `TitleSpan` and update all targets |
| MoveBranch | Deferred; remove and re-insert |

Inserted templates must respect the exit block's current mode and keep a fallback.

### 5.5 Invariants after each command

- Mode is consistent: choice branches have labels, automatic branches do not.
- At least one unconditional branch exists.
- Every module has an exit block.
- Targets exist and `END` is used correctly.
- Variables are declared in `@globals`.
- Titles are unique.

Commands that violate an invariant should be rejected or auto-repaired before writing.

### 5.6 Comments and whitespace

- Deleting a branch removes the whole line, including the newline.
- New branches use a standard template.
- Text outside the edited span keeps its original formatting.
- Moving a branch with comments may lose the comment association in v0.
- A lightweight CST/trivia model can be added if lossless moves become necessary.

### 5.7 When to introduce a CST

Only when one of these becomes necessary:

- incremental re-parsing for large files;
- lossless formatting / pretty-printing;
- comment-preserving refactors and moves;
- structure editing that must reproduce the source byte-for-byte.

Until then, AST + Span + SourceText + TextEdit is sufficient.

## 6. Build and compatibility

- `Directory.Build.props`: nullable, implicit usings, latest LangVersion.
- `.editorconfig`: formatting and C# style.
- `.gitignore`: `bin/`, `obj/`, IDE and test artifacts.
- `global.json`: pins the .NET 10 feature band; `rollForward: latestFeature` allows patch SDKs.
- `net10.0`: .NET 10 is the current LTS, supported until November 2028. .NET 8 LTS ends in November 2026.
- Release builds are self-contained, so users do not install a runtime.
- The language core stays on `net10.0`; multi-targeting `net8.0` / `netstandard2.1` is deferred until engine reuse is required.

## 7. Testing strategy

| Level | Scope |
|---|---|
| Unit | Lexing, parsing, conditions, diagnostics |
| Integration | Complete example -> AST -> diagnostics |
| Smoke | HTML export and asset embedding (later) |

M1 acceptance:

- The complete example parses.
- Forward module references resolve.
- Undeclared variables, missing targets, missing exits, mixed modes, and missing fallbacks are reported.
- Tests pass.

## 8. Current status

- Solution and project skeleton are in place.
- Language, architecture, and compiler walkthrough docs are in place.
- **Parser front-end is implemented**: SourceText, diagnostics, AST, globals/module/content/exit parsing, CLI, tests.
- `@next` exits are parsed into branches, conditions, and effects. StoryGraph and cross-module validation are not connected yet.
