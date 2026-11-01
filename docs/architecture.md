# 项目架构

本文描述工程结构与模块职责。语言语义见 [language-spec.md](./language-spec.md)。

---

## 1. 定位

Weftline 是**剧情 demo / 叙事演示工具**，不是游戏引擎，也不是商业剧情生产管线。

它负责：

- 快速写出可玩剧情 demo；
- 把剧情结构可视化为图；
- 导出零安装、可在浏览器中演示的 HTML。

生产管线能力（立绘、音频、存档、本地化、语音、引擎触发等）不在范围内。

---

## 2. 分层

```
                ┌──────────────────────┐
                │      Weftline.App    │  Avalonia UI
                │  编辑器 / 图视图 / 面板 │
                └──────────┬───────────┘
                           │
            ┌──────────────┴──────────────┐
            │                             │
   ┌────────▼────────┐          ┌─────────▼────────┐
   │ Weftline.Export │          │ Weftline.Language │
   │ HTML / SVG 导出  │─────────>│ 解析 / 诊断 / IR   │
   └─────────────────┘          └───────────────────┘
```

依赖规则：

- `Weftline.Language` 不引用任何 UI 或导出代码。
- `Weftline.Export` 只依赖 `Weftline.Language`。
- `Weftline.App` 可以依赖两者。
- 测试项目只依赖被测项目。

---

## 3. 项目职责

### 3.1 Weftline.Language

语言核心，纯 C# 类库，零 UI 依赖。

计划内部结构：

```
Source/
  SourceText.cs          源文本、行索引、偏移
  TextSpan.cs            起止位置
Syntax/
  Token.cs
  Lexer.cs
  Parser.cs              行式解析
  Ast/                   模块、globals、内容块、出口、条件
Binding/
  Binder.cs              模块 / 变量 / 角色注册与解析
  Validator.cs           图与语义校验
  Symbols.cs
Ir/
  StoryGraph.cs          节点、出口、全局变量、入口
Diagnostics/
  Diagnostic.cs
  DiagnosticBag.cs
```

职责：

- 解析 `.weft` 源文本，产出 AST 与 StoryGraph。
- 产生结构化诊断（位置、级别、错误码）。
- 支持前向引用：两阶段解析。
- 支持单模块隔离编译。
- 不处理 UI、布局、HTML。

### 3.2 Weftline.Export

- 消费 `StoryGraph`，生成单文件 HTML。
- 内嵌图片资源。
- 内置轻量 JS 播放器：文本推进、选择、条件、布尔状态、结束。
- 不依赖 Avalonia。

### 3.3 Weftline.App

Avalonia 桌面应用。

计划结构：

```
Views/           MainWindow、编辑器、图视图
ViewModels/      主视图、文档、状态面板
Controls/        图视图自绘控件
Services/        文件、自动保存、资源导入、布局侧车
```

职责：

- 文本编辑器（AvaloniaEdit）与图视图并排。
- 出口分支自动缩进、模块标题/角色/变量补全。
- 播放预览与导出入口。
- 图片导入与资源面板。
- 布局坐标持久化（侧车文件）。

### 3.4 Weftline.Language.Tests

- 解析器与诊断的单元测试。
- 使用内联源文本，避免依赖文件系统。
- 后续增加 IR 快照测试和导出冒烟测试。

### 3.5 Weftline.Cli

- 提供 `parse` 命令，把 `.weft` 解析结果打印成 AST 与诊断。
- 用于手测、学习和调试，不承载产品功能。
- 不依赖 Avalonia。

---

## 4. 数据流

```
.weft 源文件
   │
   ▼
Parser ──> AST
   │
   ▼
Binder / Validator ──> StoryGraph IR + Diagnostics
   │
   ├──> Weftline.App    图视图 / 预览
   └──> Weftline.Export 单文件 HTML
```

关键原则：

- 源文本是唯一真相。
- 图是文本的投影，不反向生成整份源码。
- 布局坐标独立存储，不进入语言与 IR。
- 诊断始终带源码位置。

---

## 5. 编辑模型（图 → 源码写回）

### 5.1 原则

- 源文本是唯一文档，AST 节点带 `TextSpan`。
- v0 不引入 CST；手动连接不需要反编译。
- GUI 操作是**预定义命令**，每个命令计算一组最小 `TextEdit`。
- 文本编辑与 GUI 编辑串行作用于同一份 `SourceText`，然后重新解析。
- Undo/Redo 基于文本编辑栈。

### 5.2 管线

```
GUI 命令
  → IEditCommand.Compute(...)
  → TextEdit[]（Span + 新文本）
  → 应用到 SourceText
  → 重新解析
  → 刷新 StoryGraph / Diagnostics / 图视图
```

### 5.3 需要的锚点

AST / 符号表至少要提供：

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
SourceText 的行索引
```

### 5.4 命令与文本操作

| 命令 | 文本操作 |
|---|---|
| AddEdge | 在出口块插入点插入一行分支模板 |
| RemoveEdge | 删除该分支整行（含换行） |
| RetargetEdge | 替换 `TargetSpan` |
| EditCondition | 替换/插入 `ConditionSpan` |
| EditLabel | 替换 `LabelSpan` |
| AddEffect / RemoveEffect | 修改 `{}` 内的 `@set` |
| AddModule | 在文档末尾或选中模块后插入模块模板 |
| DeleteModule | 删除模块整段 `Span` |
| RenameModule | 替换 `TitleSpan`，并同步所有指向它的目标 |
| MoveBranch | 暂缓；先删除 + 插入 |

插入模板必须遵守出口块的当前模式（选择 / 自动），并保持兜底。

### 5.5 命令后的不变量

每个命令执行后都要重新校验：

- 模式一致：选择模式全带文案，自动模式全不带；
- 至少一条无条件分支（兜底）；
- 每个模块都有出口块；
- 目标存在，`END` 未被非法使用；
- 变量在 `@globals` 中已声明；
- 标题唯一。

违反不变量的命令应被拒绝，或由命令自动修复后再落盘。

### 5.6 注释与空白策略

- 删除分支时删除整行，包括行尾换行。
- 新增分支使用统一模板。
- 除被编辑的 Span 外，其余原文与排版保持不变。
- 移动带注释的分支时，注释归属可能丢失；v0 接受。
- 如果需要无损保留注释与排版，再引入轻量 CST / trivia。

### 5.7 何时升级到 CST

只有出现以下需求时才升级：

- 增量重解析（大文件编辑性能）；
- 无损格式化 / pretty-print；
- 注释保留的复杂重构与移动；
- 结构化编辑需要逐字节还原源码。

在此之前，AST + Span + SourceText + TextEdit 足够。

---

## 6. 构建与兼容性

- `Directory.Build.props`：启用 nullable、implicit usings、latest LangVersion。
- `.editorconfig`：统一格式与 C# 风格。
- `.gitignore`：忽略 `bin/`、`obj/`、IDE 与测试产物。
- `global.json`：锁定 .NET 10 特性带，`rollForward: latestFeature` 允许补丁 SDK。
- 目标框架 `net10.0`：.NET 10 是当前 LTS，支持到 2028 年 11 月；.NET 8 的 LTS 支持在 2026 年 11 月结束。
- 发布使用 self-contained，避免用户单独安装运行时。
- 语言核心保持 `net10.0`；若将来需要引擎内复用，再考虑多目标 `net8.0` / `netstandard2.1`。

---

## 7. 测试策略

| 层级 | 内容 |
|---|---|
| 单元 | 词法、解析、条件表达式、诊断 |
| 集成 | 完整示例 -> StoryGraph -> 诊断 |
| 冒烟 | 导出 HTML 可生成、资源可内嵌（后续） |

M1 的验收标准：

- 完整示例可解析。
- 前向模块引用可解析。
- 未声明变量、缺失目标、缺失出口、混用模式、缺少兜底能正确报错。
- 测试通过。

---

## 8. 当前状态

- 解决方案与项目骨架已创建。
- 语言文档、架构文档、编译器入门文档已就位。
- **Step 1 编译器前端已实现**：SourceText、诊断、AST、globals/模块/内容解析、CLI、测试。
- `@next` 出口块目前只保留原文；Step 2 再解析分支、条件、效果，并构建 StoryGraph。
