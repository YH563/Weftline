# 代码阅读顺序

[**中文**](./reading-guide.md) | [English](./en/reading-guide.md)

本文给出一条从语言核心到桌面端的完整阅读路线，覆盖 native C++、C# 互操作、Core、App、Widgets、测试与基准。第一次阅读建议按阶段顺序走；做具体功能时可以直接跳到对应章节。

---

## 0. 先建立全局地图

1. [README.md](../README.md) / [README.en.md](../README.en.md) — 产品定位与当前能力。
2. [AGENTS.md](../AGENTS.md) — 构建、测试、代码约定与 UI 预览方式。
3. [docs/language-spec.md](./language-spec.md) — `.weft` / `.wstory` 的语义。
4. [docs/grammar.md](./grammar.md) — 词法与 EBNF。
5. [docs/diagnostics.md](./diagnostics.md) — 诊断码。
6. `docs/design-local/architecture.md`（本地内部文档）— 分层与数据流。
7. [docs/building.md](./building.md) — 构建、测试、基准、截图。

记住整体数据流：

```text
.weft/.wstory 文本
  -> C++ Lexer/Parser/Binder/Validator
  -> ProjectModel / StoryGraph / GraphAnalysis
  -> GraphProjection / RuntimeProjection
  -> C ABI JSON
  -> C# Native 模型
  -> Core 图分析 / 播放器状态机
  -> App ViewModel / View
```

---

## 1. 契约与边界

先看跨语言契约，再看实现：

1. `interop/contract/parse.response.schema.json`
2. `interop/contract/project.response.schema.json`（v4，runtime 已内联）
3. `interop/contract/README.md` — 响应约定：版本、数组策略、内存所有权
4. `native/weftline-core/include/weftline/core.h` — 唯一对外 C ABI。

当前 C ABI 只保留：

- `weft_version` / `weft_free`
- `weft_parse_json`
- `weft_project_create(folder)` / `destroy` / `set_document` / `remove_document`
- `weft_project_compile(project, flags)`

flags：`PARTIAL` / `PROJECTION` / `RUNTIME`。诊断和符号始终返回；图/投影和 runtime 按需返回。

阅读重点：`schemaVersion`、版本回显、`error/hasErrors`、项目句柄生命周期、`weft_free` 的所有权。

---

## 2. C++ 核心：从文本到 IR

### 2.1 基础设施

1. `native/weftline-core/include/weftline/support/box.hpp`
2. `native/weftline-core/include/weftline/support/overloaded.hpp`
3. `native/weftline-core/include/weftline/source/location.hpp`
4. `native/weftline-core/include/weftline/source/text_utils.hpp`
5. `native/weftline-core/include/weftline/source/source_text.hpp`
6. `native/weftline-core/src/source/source_text.cpp`
7. `native/weftline-core/include/weftline/diagnostics/diagnostic.hpp`
8. `native/weftline-core/src/diagnostics/diagnostic.cpp`
9. `native/weftline-core/include/weftline/json/json_writer.hpp`

重点：UTF-16 偏移、行列换算、诊断不抛异常、JSON 输出。

### 2.2 词法与语法

1. `include/weftline/syntax/token.hpp`
2. `include/weftline/syntax/lexer.hpp` → `src/syntax/lexer.cpp`
3. `include/weftline/syntax/syntax_facts.hpp`
4. `include/weftline/syntax/ast.hpp`
5. `include/weftline/syntax/parser.hpp` → `src/syntax/parser.cpp`
6. `include/weftline/syntax/condition_parser.hpp` → `src/syntax/condition_parser.cpp`
7. `include/weftline/syntax/ast_printer.hpp` → `src/syntax/ast_printer.cpp`

`parser.cpp` 是最大的单文件，按“文件级 → 声明区 → 模块 → 正文块 → 出口块”顺序读。

### 2.3 绑定

1. `include/weftline/binding/symbol_table.hpp` → `src/binding/symbol_table.cpp`
2. `include/weftline/binding/binder.hpp` → `src/binding/binder.cpp`

重点：单文件符号、引用解析、变量初始值、章节/模块归属。

### 2.4 项目层

1. `include/weftline/projects/project_snapshot.hpp`
2. `include/weftline/projects/project_loader.hpp` → `src/projects/project_loader.cpp`
3. `include/weftline/projects/project_index.hpp`
4. `include/weftline/projects/project_binder.hpp` → `src/projects/project_binder.cpp`
5. `include/weftline/projects/validator.hpp` → `src/projects/validator.cpp`
6. `include/weftline/projects/project_entry.hpp` → `src/projects/project_entry.cpp`
7. `include/weftline/projects/compilation_cache.hpp` → `src/projects/compilation_cache.cpp`
8. `include/weftline/projects/project_compiler.hpp` → `src/projects/project_compiler.cpp`

重点：扁平目录、`.weft` 是声明唯一来源、跨文件前向引用、入口候选、END 可达性、增量缓存。

### 2.5 语义 IR 与图

1. `include/weftline/ir/project_model.hpp` → `src/ir/project_model_builder.cpp`
2. `include/weftline/ir/story_graph.hpp` → `src/ir/story_graph_builder.cpp`
3. `include/weftline/ir/graph_analysis.hpp` → `src/ir/graph_analysis.cpp`、`src/ir/graph_analysis_builder.cpp`
4. `include/weftline/ir/graph_projection.hpp` → `src/ir/graph_projection_builder.cpp`
5. `include/weftline/ir/runtime_projection.hpp` → `src/ir/runtime_projection_builder.cpp`
6. `include/weftline/ir/ir_json.hpp` → `src/ir/ir_json.cpp`

五个 `build_*` 入口统一声明在 `include/weftline/ir/builders.hpp`，按 ProjectModel → StoryGraph → GraphAnalysis → GraphProjection 的顺序排列；实现仍分在各自的 `*_builder.cpp`。

重点：`ProjectModel` 是语义汇总；`StoryGraph` 是未压缩图；`GraphProjection` 是 UI 投影；`RuntimeProjection` 是播放器载荷。

### 2.6 代码生成

1. `include/weftline/generation/wstory_generator.hpp` → `src/generation/wstory_generator.cpp`

重点：从 `.weft` 生成 `.wstory` 时如何保留手写描述。

### 2.7 C ABI 与 CLI

1. `include/weftline/tools/lexer_json.hpp`
2. `src/tools/lexer_json.cpp`
3. `src/api/core.cpp`
4. `src/api/parse_api.cpp`
5. `src/api/project_api.cpp`
6. `src/main.cpp`

重点：所有 C++ 异常必须在 ABI 边界内收敛；返回值由 `weft_free` 释放；项目句柄不是线程安全的。

---

## 3. C# Native 桥

1. `src/Weftline.Native/NativeMethods.cs` — `LibraryImport`。
2. `src/Weftline.Native/NativeModels.cs` — 编译/项目响应。
3. `src/Weftline.Native/NativeRuntimeModels.cs` — 播放器载荷。
4. `src/Weftline.Native/INativeProjectClient.cs`
5. `src/Weftline.Native/NativeWeftlineClient.cs` — 句柄、文档增量、编译、runtime、`Dispose`。
6. `src/Weftline.Native/NativeWeftSyntaxParser.cs`
7. `src/Weftline.Native/Weftline.Native.csproj` — 动态库复制。

重点：native 句柄所有权、`LibraryImport`、JSON↔C# 模型映射、动态库路径。

---

## 4. Weftline.Core

### 4.1 语言与领域模型

1. `src/Weftline.Core/Language/IWeftSyntaxParser.cs`
2. `src/Weftline.Core/Language/IWeftEditorLanguageService.cs`
3. `src/Weftline.Core/Language/EditorLanguageService.cs`
4. `src/Weftline.Core/Language/WeftDiagnosticSeverityMapping.cs`
5. `src/Weftline.Core/SourceModels.cs`
6. `src/Weftline.Core/WeftDiagnostic.cs`
7. `src/Weftline.Core/WeftProjectSymbols.cs`
8. `src/Weftline.Core/WeftCompletion.cs`
9. `src/Weftline.Core/WeftHoverInfo.cs`

### 4.2 图与播放器

1. `src/Weftline.Core/GraphProjection.cs`
2. `src/Weftline.Core/GraphAnalysisModels.cs`
3. `src/Weftline.Core/GraphFilterSelection.cs`
4. `src/Weftline.Core/StoryGraphModels.cs`
5. `src/Weftline.Core/Graph/Analysis/GraphPathHighlight.cs`
6. `src/Weftline.Core/Graph/Analysis/GraphFilterMatch.cs`
7. `src/Weftline.Core/Runtime/RuntimeProjection.cs`
8. `src/Weftline.Core/Runtime/PlayerSession.cs`

`PlayerSession` 是理解“剧本如何播放”的核心：逐句推进、条件、效果、选择、自动分支、错误状态。

---

## 5. Weftline.App：组合根与服务

先看应用如何组装，再看功能：

1. `src/Weftline.App/Program.cs` — 入口、启动参数。
2. `src/Weftline.App/Tools/ShotRunner.cs` — 离屏截图。
3. `src/Weftline.App/App.axaml` / `App.axaml.cs` — 应用生命周期。
4. `src/Weftline.App/Services/AppComposition.cs` — 唯一创建具体实现的地方。
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

重点：分析调度（防抖、单飞、取消、latest-wins）、文档快照缓存、原子保存、外部变更、日志。

---

## 6. 编辑器

按“文本模型 → 主机 → 调度 → 控件 → 视图”顺序：

1. `Features/Editor/Services/IEditorDocumentLoader.cs`
2. `Services/FileEditorDocumentLoader.cs`
3. `Features/Editor/ViewModels/EditorDocumentViewModel.cs`
4. `Features/Editor/ViewModels/EditorHostViewModel.cs`
5. `Features/Editor/Services/SyntaxAnalysisScheduler.cs`
6. `Features/Editor/Services/WeftDocumentKinds.cs`
7. `Features/Editor/Services/IWeftEditorLanguageService.cs`（实际定义在 Core/Language）
8. `Features/Editor/Controls/DiagnosticRenderer.cs`
9. `Features/Editor/Controls/HoverController.cs`
10. `Features/Editor/Controls/CompletionController.cs`
11. `Features/Editor/Controls/WeftLineNumberMargin.cs`
12. `Features/Editor/Rendering/EditorTheme.cs`
13. `Features/Editor/Views/WeftEditorView.axaml` / `.axaml.cs`
14. `Features/Editor/Highlighting/Weft.xshd`
15. `Features/Editor/Themes/EditorStyles.axaml`

重点：`TextDocument` 是唯一文本源；语法诊断与项目诊断在文档层合并；标签切换保留光标/选区/滚动。

---

## 7. 剧情图

1. `Features/Graph/GraphProjectionSignature.cs`
2. `Features/Graph/Layout/GraphLayout.cs`
3. `Features/Graph/Layout/GraphLayoutOptions.cs`
4. `Features/Graph/Layout/GraphLayoutEngine.cs`
5. `Features/Graph/Layout/GraphLayoutSnapshot.cs`
6. `Features/Graph/Rendering/GraphTheme.cs`
7. `Features/Graph/Controls/GraphInteractionEventArgs.cs`
8. `Features/Graph/Controls/StoryGraphView.cs` — 属性、缓存、生命周期、Render 入口
9. `StoryGraphView.Selection.cs` — 选中与路径/筛选高亮
10. `StoryGraphView.Layout.cs` — 重置、导入导出、结构比较、边界约束
11. `StoryGraphView.HitTesting.cs` — 命中测试、连线几何
12. `StoryGraphView.Input.cs` — 指针、拖拽、缩放
13. `StoryGraphView.Rendering.cs` — 网格、章节、模块、连线、文本
14. `Features/Graph/ViewModels/GraphViewModel.cs`
15. `Features/Graph/ViewModels/GraphFilterOption.cs`

重点：图控件只消费 `GraphProjection`；自动布局与手工覆盖分离；`GraphProjectionSignature` 避免无变化重建。

---

## 8. 播放器与预览面板

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

重点：`PreviewViewModel` 是独立窗口状态机；`PreviewPanelViewModel` 是右侧只读文件预览；图片缓存只允许项目目录。

---

## 9. 主窗口壳

`MainWindowViewModel` 按 partial 顺序读：

1. `ViewModels/MainWindowViewModel.cs` — 字段、构造、属性、事件
2. `ViewModels/MainWindowViewModel.Project.cs` — 打开项目、保存、布局、菜单命令
3. `ViewModels/MainWindowViewModel.Compile.cs` — 编译调度、结果应用、诊断
4. `ViewModels/MainWindowViewModel.Editor.cs` — 文档/光标/文件系统事件
5. `ViewModels/MainWindowViewModel.Graph.cs` — 图导航与悬停
6. `ViewModels/MainWindowViewModel.Preview.cs` — 预览命令与可见性
7. `ViewModels/MainWindowViewModel.Lifecycle.cs` — 未保存确认、外部变更、释放

然后读：

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

重点：App 只做视图、绑定和交互；业务状态在 ViewModel / Core。

---

## 10. 设计系统

1. `src/Weftline.Widgets/Themes/Tokens.axaml`
2. `src/Weftline.Widgets/Themes/WeftlineTheme.axaml`
3. `src/Weftline.Widgets/Themes/WeftlineStyles.axaml`
4. `src/Weftline.Widgets/Themes/Input.axaml`
5. `src/Weftline.Widgets/Themes/List.axaml`
6. `src/Weftline.Widgets/Themes/Menu.axaml`
7. `src/Weftline.Widgets/Themes/Overlay.axaml`

改界面时先读令牌，再读控件样式，最后读具体 View。

---

## 11. 测试与基准

### C# 测试

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

### C++ 测试

按依赖顺序：

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

### 基准

1. `bench/Weftline.Benchmarks/Program.cs`
2. `bench/Weftline.Benchmarks/Weftline.Benchmarks.csproj`

---

## 12. 按任务选路线

- **改语言/解析/诊断**：第 2 章 → 第 3 章 → 第 4.1 章 → 第 11 章 C++ 测试 → `docs/language-spec.md`、`docs/grammar.md`、`docs/diagnostics.md`。
- **改图投影/图分析**：第 2.5 章 → 第 3 章 → 第 4.2 章 → 第 7 章 → `tests/Weftline.Core.Tests/GraphAnalysisTests.cs`。
- **改编辑器**：第 5 章 → 第 6 章 → `Features/Editor/Views` → 第 11 章 C# 测试。
- **改播放器**：第 4.2 章 `PlayerSession` → 第 8 章 → `PlayerSessionTests`。
- **改主窗口/交互**：第 5 章 → 第 9 章 → `MainWindowViewModelTests`、`MenuInteractionTests`，再用 `./preview.sh` 对比。
- **改视觉**：第 10 章 → 第 9 章对应 View → `./preview.sh` / `./preview.sh --dark`。
- **改 ABI**：第 1 章 → 第 2.7 章 → 第 3 章 → 同步更新 `interop/contract`、C#/C++ 模型与测试。
