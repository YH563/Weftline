# Code Reading Guide

[**中文**](../reading-guide.md) | [English](./reading-guide.md)

This guide gives a complete reading path from the native language core to the desktop app. It covers native C++, the C# interop layer, Core, App, Widgets, tests, and benchmarks. Read it in order the first time; jump to a chapter when working on a specific feature.

---

## 0. Build the global map

1. [README.en.md](../../README.en.md) / [README.md](../../README.md) — positioning and current capabilities.
2. [AGENTS.md](../../AGENTS.md) — build, test, conventions, and UI preview workflow.
3. [language-spec.md](./language-spec.md) — `.weft` / `.wstory` semantics.
4. [grammar.md](./grammar.md) — lexer and EBNF.
5. [diagnostics.md](./diagnostics.md) — diagnostic codes.
6. `docs/design-local/architecture.md` (local-only internal doc) — layering and data flow.
7. [building.md](./building.md) — build, test, benchmark, screenshots.

Keep the data flow in mind:

```text
.weft/.wstory text
  -> C++ Lexer/Parser/Binder/Validator
  -> ProjectModel / StoryGraph / GraphAnalysis
  -> GraphProjection / RuntimeProjection
  -> C ABI JSON
  -> C# Native models
  -> Core graph analysis / player state machine
  -> App ViewModel / View
```

---

## 1. Contracts and boundaries

Read the cross-language contracts before implementations:

1. `interop/contract/compile.request.schema.json`
2. `interop/contract/compile.response.schema.json`
3. `interop/contract/parse.response.schema.json`
4. `interop/contract/project.response.schema.json`
5. `interop/contract/runtime.response.schema.json`
6. `native/weftline-core/include/weftline/core.h` — the only exported C ABI.

Watch for `schemaVersion`, version echo, `error/hasErrors`, project-handle lifetime, and `weft_free` ownership.

---

## 2. C++ core: text to IR

### 2.1 Foundations

1. `native/weftline-core/include/weftline/support/box.hpp`
2. `native/weftline-core/include/weftline/support/overloaded.hpp`
3. `native/weftline-core/include/weftline/source/location.hpp`
4. `native/weftline-core/include/weftline/source/text_utils.hpp`
5. `native/weftline-core/include/weftline/source/source_text.hpp`
6. `native/weftline-core/src/source/source_text.cpp`
7. `native/weftline-core/include/weftline/diagnostics/diagnostic.hpp`
8. `native/weftline-core/src/diagnostics/diagnostic.cpp`
9. `native/weftline-core/include/weftline/json/json_writer.hpp`

Focus: UTF-16 offsets, line/column conversion, diagnostics never throwing, JSON output.

### 2.2 Lexer and parser

1. `include/weftline/syntax/token.hpp`
2. `include/weftline/syntax/lexer.hpp` → `src/syntax/lexer.cpp`
3. `include/weftline/syntax/syntax_facts.hpp`
4. `include/weftline/syntax/ast.hpp`
5. `include/weftline/syntax/parser.hpp` → `src/syntax/parser.cpp`
6. `include/weftline/syntax/condition_parser.hpp` → `src/syntax/condition_parser.cpp`
7. `include/weftline/syntax/ast_printer.hpp` → `src/syntax/ast_printer.cpp`

`parser.cpp` is the largest file. Read it as: file header → declaration area → module → prose block → exit block.

### 2.3 Binding

1. `include/weftline/binding/symbol_table.hpp` → `src/binding/symbol_table.cpp`
2. `include/weftline/binding/binder.hpp` → `src/binding/binder.cpp`

Focus: per-file symbols, reference resolution, variable initial values, chapter/module ownership.

### 2.4 Project layer

1. `include/weftline/projects/project_snapshot.hpp`
2. `include/weftline/projects/project_loader.hpp` → `src/projects/project_loader.cpp`
3. `include/weftline/projects/project_index.hpp`
4. `include/weftline/projects/project_binder.hpp` → `src/projects/project_binder.cpp`
5. `include/weftline/projects/validator.hpp` → `src/projects/validator.cpp`
6. `include/weftline/projects/project_entry.hpp` → `src/projects/project_entry.cpp`
7. `include/weftline/projects/compilation_cache.hpp` → `src/projects/compilation_cache.cpp`
8. `include/weftline/projects/project_compiler.hpp` → `src/projects/project_compiler.cpp`

Focus: flat directory, `.weft` as the only declaration source, forward references, entry candidates, END reachability, incremental cache.

### 2.5 Semantic IR and graph

1. `include/weftline/ir/project_model.hpp` → `src/ir/project_model_builder.cpp`
2. `include/weftline/ir/story_graph.hpp` → `src/ir/story_graph_builder.cpp`
3. `include/weftline/ir/graph_analysis.hpp` → `src/ir/graph_analysis.cpp`, `src/ir/graph_analysis_builder.cpp`
4. `include/weftline/ir/graph_projection.hpp` → `src/ir/graph_projection_builder.cpp`
5. `include/weftline/ir/runtime_projection.hpp` → `src/ir/runtime_projection_builder.cpp`
6. `include/weftline/ir/ir_json.hpp` → `src/ir/ir_json.cpp`

Focus: `ProjectModel` is the semantic summary; `StoryGraph` is the uncompressed graph; `GraphProjection` is the UI projection; `RuntimeProjection` is the player payload.

### 2.6 Generation

1. `include/weftline/generation/wstory_generator.hpp` → `src/generation/wstory_generator.cpp`

Focus: how `.wstory` generation preserves hand-written descriptions.

### 2.7 C ABI and CLI

1. `include/weftline/api/compile.hpp`
2. `src/api/compile.cpp`
3. `src/api/core.cpp`
4. `src/api/parse_api.cpp`
5. `src/api/project_api.cpp`
6. `src/main.cpp`

Focus: all C++ exceptions must be contained inside the ABI boundary; returned memory is released through `weft_free`; project handles are not thread-safe.

---

## 3. C# Native bridge

1. `src/Weftline.Native/NativeMethods.cs` — `LibraryImport`.
2. `src/Weftline.Native/NativeModels.cs` — compile/project responses.
3. `src/Weftline.Native/NativeRuntimeModels.cs` — player payload.
4. `src/Weftline.Native/INativeProjectClient.cs`
5. `src/Weftline.Native/NativeWeftlineClient.cs` — handle, incremental documents, compile, runtime, `Dispose`.
6. `src/Weftline.Native/NativeWeftSyntaxParser.cs`
7. `src/Weftline.Native/Weftline.Native.csproj` — native library copy.

Focus: handle ownership, `LibraryImport`, JSON↔C# model mapping, dynamic library paths.

---

## 4. Weftline.Core

### 4.1 Language and domain models

1. `src/Weftline.Core/Language/IWeftSyntaxParser.cs`
2. `src/Weftline.Core/Language/IWeftEditorLanguageService.cs`
3. `src/Weftline.Core/Language/EditorLanguageService.cs`
4. `src/Weftline.Core/Language/WeftDiagnosticSeverityMapping.cs`
5. `src/Weftline.Core/SourceModels.cs`
6. `src/Weftline.Core/WeftDiagnostic.cs`
7. `src/Weftline.Core/WeftProjectSymbols.cs`
8. `src/Weftline.Core/WeftCompletion.cs`
9. `src/Weftline.Core/WeftHoverInfo.cs`

### 4.2 Graph and player

1. `src/Weftline.Core/GraphProjection.cs`
2. `src/Weftline.Core/GraphAnalysisModels.cs`
3. `src/Weftline.Core/GraphFilterSelection.cs`
4. `src/Weftline.Core/StoryGraphModels.cs`
5. `src/Weftline.Core/Graph/Analysis/GraphPathHighlight.cs`
6. `src/Weftline.Core/Graph/Analysis/GraphFilterMatch.cs`
7. `src/Weftline.Core/Runtime/RuntimeProjection.cs`
8. `src/Weftline.Core/Runtime/PlayerSession.cs`

`PlayerSession` is the key to understanding playback: advance, conditions, effects, choices, auto branches, and error states.

---

## 5. Weftline.App: composition root and services

Read how the app is assembled before reading features:

1. `src/Weftline.App/Program.cs` — entry and CLI flags.
2. `src/Weftline.App/Tools/ShotRunner.cs` — headless screenshots.
3. `src/Weftline.App/App.axaml` / `App.axaml.cs` — application lifetime.
4. `src/Weftline.App/Services/AppComposition.cs` — the only place that creates concrete implementations.
5. `src/Weftline.App/Services/MainWindowServices.cs`
6. `src/Weftline.App/Services/IProjectAnalysisScheduler.cs`
7. `src/Weftline.App/Services/ProjectAnalysisScheduler.cs`
8. `src/Weftline.App/Services/IUiDispatcher.cs`
9. `src/Weftline.App/Services/DocumentSnapshotProvider.cs`
10. `src/Weftline.App/Services/IDocumentSaver.cs`
11. `src/Weftline.App/Services/IProjectLayoutStore.cs` / `ProjectLayoutStore.cs`
12. `src/Weftline.App/Services/DiagnosticPresentation.cs`
13. `src/Weftline.App/Services/ProjectFolderWatcher.cs`
14. `src/Weftline.App/Services/AppLog.cs`
15. `src/Weftline.App/Services/IDialogService.cs` / `DialogService.cs`
16. `src/Weftline.App/Services/ThemeService.cs` / `ThemePalette.cs`
17. `src/Weftline.App/Services/EditorIconProvider.cs` / `EditorThemeSource.cs` / `FileIconResolver.cs`

Focus: analysis scheduling (debounce, single-flight, cancellation, latest-wins), snapshot cache, atomic save, external changes, logging.

---

## 6. Editor

Read in the order: text model → host → scheduler → controls → view:

1. `Features/Editor/Services/IEditorDocumentLoader.cs`
2. `Services/FileEditorDocumentLoader.cs`
3. `Features/Editor/ViewModels/EditorDocumentViewModel.cs`
4. `Features/Editor/ViewModels/EditorHostViewModel.cs`
5. `Features/Editor/Services/SyntaxAnalysisScheduler.cs`
6. `Features/Editor/Services/WeftDocumentKinds.cs`
7. `Features/Editor/Services/IWeftEditorLanguageService.cs` (defined in Core/Language)
8. `Features/Editor/Controls/DiagnosticRenderer.cs`
9. `Features/Editor/Controls/HoverController.cs`
10. `Features/Editor/Controls/CompletionController.cs`
11. `Features/Editor/Controls/WeftLineNumberMargin.cs`
12. `Features/Editor/Rendering/EditorTheme.cs`
13. `Features/Editor/Views/WeftEditorView.axaml` / `.axaml.cs`
14. `Features/Editor/Highlighting/Weft.xshd`
15. `Features/Editor/Themes/EditorStyles.axaml`

Focus: `TextDocument` is the only text source; syntax and project diagnostics merge at the document layer; tab switching preserves caret, selection, and scroll.

---

## 7. Story graph

1. `Features/Graph/GraphProjectionSignature.cs`
2. `Features/Graph/Layout/GraphLayout.cs`
3. `Features/Graph/Layout/GraphLayoutOptions.cs`
4. `Features/Graph/Layout/GraphLayoutEngine.cs`
5. `Features/Graph/Layout/GraphLayoutSnapshot.cs`
6. `Features/Graph/Rendering/GraphTheme.cs`
7. `Features/Graph/Controls/GraphInteractionEventArgs.cs`
8. `Features/Graph/Controls/StoryGraphView.cs` — properties, caches, lifetime, Render entry
9. `StoryGraphView.Selection.cs` — selection and path/filter highlights
10. `StoryGraphView.Layout.cs` — reset, import/export, structure comparison, bounds
11. `StoryGraphView.HitTesting.cs` — hit testing and wire geometry
12. `StoryGraphView.Input.cs` — pointer, drag, zoom
13. `StoryGraphView.Rendering.cs` — grid, chapters, modules, wires, text
14. `Features/Graph/ViewModels/GraphViewModel.cs`
15. `Features/Graph/ViewModels/GraphFilterOption.cs`

Focus: the control consumes `GraphProjection` only; automatic layout and manual overrides are separate; `GraphProjectionSignature` avoids rebuilding unchanged projections.

---

## 8. Player and preview panel

1. `src/Weftline.Core/Runtime/RuntimeProjection.cs`
2. `src/Weftline.Core/Runtime/PlayerSession.cs`
3. `Features/Player/ViewModels/PreviewViewModel.cs`
4. `Features/Player/ViewModels/PreviewPanelViewModel.cs`
5. `Features/Player/ViewModels/PreviewImageCache.cs`
6. `Features/Player/ViewModels/PreviewModuleViewModel.cs`
7. `Features/Player/ViewModels/PreviewBlockViewModel.cs`
8. `Features/Player/ViewModels/PreviewLineViewModel.cs`
9. `Features/Player/ViewModels/PreviewBranchViewModel.cs`
10. `Features/Player/ViewModels/PreviewChoiceViewModel.cs`
11. `Features/Player/Views/PreviewWindow.axaml` / `.axaml.cs`
12. `Features/Player/Views/PreviewPanelView.axaml` / `.axaml.cs`

Focus: `PreviewViewModel` drives the standalone player window; `PreviewPanelViewModel` is the read-only file preview on the right; image loading is restricted to the project folder.

---

## 9. Main window shell

Read `MainWindowViewModel` partials in order:

1. `ViewModels/MainWindowViewModel.cs` — fields, constructor, properties, events
2. `ViewModels/MainWindowViewModel.Project.cs` — project open, save, layout, menu commands
3. `ViewModels/MainWindowViewModel.Compile.cs` — compile scheduling, result application, diagnostics
4. `ViewModels/MainWindowViewModel.Editor.cs` — document/caret/filesystem events
5. `ViewModels/MainWindowViewModel.Graph.cs` — graph navigation and hover
6. `ViewModels/MainWindowViewModel.Preview.cs` — preview commands and visibility
7. `ViewModels/MainWindowViewModel.Lifecycle.cs` — unsaved confirmation, external changes, disposal

Then read:

1. `ViewModels/ProjectExplorerViewModel.cs`
2. `ViewModels/ProjectTreeNodeViewModel.cs`
3. `ViewModels/StatusBarViewModel.cs`
4. `ViewModels/DiagnosticItem.cs`
5. `ViewModels/PreviewRequestedEventArgs.cs`
6. `Views/MainWindow.axaml` / `.axaml.cs`
7. `Views/MainMenuView.axaml` / `.axaml.cs`
8. `Views/ProjectExplorerView.axaml` / `.axaml.cs`
9. `Views/StatusBarView.axaml` / `.axaml.cs`
10. `Views/GraphPreviewWindow.axaml` / `.axaml.cs`
11. `Views/MessageWindow.axaml` / `.axaml.cs`
12. `Views/TextPromptWindow.axaml` / `.axaml.cs`
13. `Controls/MenuAutoCloseBehavior.cs`
14. `Controls/RoundedImage.cs`
15. `Assets/Icons.axaml`

Focus: App only does views, bindings, and interaction; business state lives in ViewModels and Core.

---

## 10. Design system

1. `src/Weftline.Widgets/Themes/Tokens.axaml`
2. `src/Weftline.Widgets/Themes/WeftlineTheme.axaml`
3. `src/Weftline.Widgets/Themes/WeftlineStyles.axaml`
4. `src/Weftline.Widgets/Themes/Input.axaml`
5. `src/Weftline.Widgets/Themes/List.axaml`
6. `src/Weftline.Widgets/Themes/Menu.axaml`
7. `src/Weftline.Widgets/Themes/Overlay.axaml`

When changing UI, read tokens first, then control styles, then the specific view.

---

## 11. Tests and benchmarks

### C# tests

1. `tests/Weftline.Core.Tests/PlayerSessionTests.cs`
2. `tests/Weftline.Core.Tests/GraphAnalysisTests.cs`
3. `tests/Weftline.App.Tests/ServiceFakes.cs`
4. `tests/Weftline.App.Tests/Fakes.cs`
5. `tests/Weftline.App.Tests/MainWindowViewModelTests.cs`
6. `tests/Weftline.App.Tests/ProjectAnalysisSchedulerTests.cs`
7. `tests/Weftline.App.Tests/SyntaxAnalysisSchedulerTests.cs`
8. `tests/Weftline.App.Tests/EditorDocumentViewModelTests.cs`
9. `tests/Weftline.App.Tests/EditorHostViewModelTests.cs`
10. `tests/Weftline.App.Tests/FileDocumentSaverTests.cs`
11. `tests/Weftline.App.Tests/DialogWindowTests.cs`
12. `tests/Weftline.App.Tests/MenuInteractionTests.cs`
13. `tests/Weftline.App.Tests/TestApp.cs` / `AssemblyInfo.cs`

### C++ tests

In dependency order:

1. `test_lexer.cpp`
2. `test_ast.cpp`
3. `test_parser.cpp`
4. `test_binder.cpp`
5. `test_wstory.cpp`
6. `test_project.cpp`
7. `test_project_compiler.cpp`
8. `test_storygraph.cpp`
9. `test_ir.cpp`
10. `test_graph_analysis.cpp`
11. `test_graph_projection.cpp`
12. `test_runtime_projection.cpp`
13. `test_ir_json.cpp`
14. `test_parse_api.cpp`
15. `test_project_api.cpp`

### Benchmarks

1. `bench/Weftline.Benchmarks/Program.cs`
2. `bench/Weftline.Benchmarks/Weftline.Benchmarks.csproj`

---

## 12. Choose a route by task

- **Language/parser/diagnostics**: chapter 2 → chapter 3 → 4.1 → C++ tests in chapter 11 → `language-spec.md`, `grammar.md`, `diagnostics.md`.
- **Graph projection/analysis**: 2.5 → chapter 3 → 4.2 → chapter 7 → `GraphAnalysisTests.cs`.
- **Editor**: chapter 5 → chapter 6 → `Features/Editor/Views` → C# tests in chapter 11.
- **Player**: 4.2 `PlayerSession` → chapter 8 → `PlayerSessionTests`.
- **Main window/interaction**: chapter 5 → chapter 9 → `MainWindowViewModelTests`, `MenuInteractionTests`, then compare with `./preview.sh`.
- **Visuals**: chapter 10 → the relevant view in chapter 9 → `./preview.sh` / `./preview.sh --dark`.
- **ABI**: chapter 1 → 2.7 → chapter 3 → update `interop/contract`, C#/C++ models, and tests together.
