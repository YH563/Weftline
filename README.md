# Weftline

**中文** | [English](README.en.md)

Weftline 是一个剧情 demo / 叙事演示工具：写剧本、看剧情图、导出可在浏览器里直接演示的 HTML。

当前阶段：语言核心可用；桌面端已具备项目资源管理器、AvaloniaEdit 多标签编辑器、语法/项目双轨实时分析、诊断合并、悬停、补全与剧情图面板。剧情图支持悬停摘要、双击跳转、章节边明细、角标和相关路径高亮；每次打开剧情图都会自动重置布局。右侧实时文件预览跟随当前 `.weft`，按模块展示正文、场景切换和出口分支，并内嵌图片。应用内剧本预览支持逐句推进、选项、条件效果、图片和局部起点。编辑 .weft/.wstory 时，单文件语法诊断与项目语义分析分别防抖调度，结果按版本应用；项目编译只增量同步变化文档，C++ 侧还会复用未变化文件的绑定和图/投影，并保留部分图。HTML 导出后续在 C++ 侧实现。

## 文件类型

| 后缀 | 角色 |
|---|---|
| `.weft` | 章节脚本：章节标题 + 顶部符号声明区（分组可选）+ 若干模块 |
| `.wstory` | 项目总览：项目信息 + 章节 + 从 `.weft` 生成的声明 + 手写描述 |

- 一个 `.weft` 文件就是一个章节。
- `.weft` 是符号声明的唯一来源。
- `.wstory` 不是声明入口；声明由工具生成，描述由作者手写并保留。
- 同一扁平文件夹下所有 `.weft` 属于同一个故事。


## 项目结构

| 项目 | 职责 |
|---|---|
| `src/Weftline.App` | 桌面 UI、窗口、视图、应用级 ViewModel 和服务 |
| `src/Weftline.Widgets` | 纯样式库：设计令牌、ControlTheme、样式 class |
| `src/Weftline.Core` | 领域模型、项目模型、图分析、播放器状态机 |
| `src/Weftline.Native` | C ABI/P/Invoke 封装；开发期原生动态库复制 |
| `src/Weftline.App/Features` | 剧情图、编辑器、剧本播放器等 UI 功能 |
| `src/Weftline.App/Views` | 只放 `.axaml` + 对应 `.axaml.cs` 视图 |
| `tests/` | `Weftline.App.Tests`、`Weftline.Core.Tests` |
| `bench/` | `Weftline.Benchmarks` |

- App 只做视图、绑定和交互；设计系统在 `Weftline.Widgets`。
- `Weftline.Core` 不引用 Avalonia。
- `Weftline.Native` 持有 native 动态库复制逻辑，App/Bench 通过项目引用传递到输出目录。

## 桌面应用

- 顶部菜单栏 + 可调整宽度的项目资源管理器 + 中央编辑区 + 底部状态栏。
- 项目资源管理器：目录/文件树、懒加载、按后缀显示图标；支持新建文件、新建文件夹、重命名、删除；通过菜单切换项目文件夹。
- 编辑器：AvaloniaEdit 多文件标签，支持关闭标签、Weft 语法高亮、行号、当前行高亮、实线分隔和未保存标记；提供语法/项目诊断合并、诊断波浪线、悬停与补全。
- 状态栏：光标行列、选区长度、语言、当前模块/场景、模块/变量数、诊断摘要和运行状态。
- 预览面板：与剧情图上下分栏，可同时打开或只保留一个；单文件跟随当前 `.weft`，按模块展示标题、场景、旁白、对白、图片和选择按钮；点击模块标题或选择项跳转源码；编译失败时保留上一次可用内容。
- 剧情图面板：实时展示当前项目投影，支持章节/模块分层、入口/出口、拖动、缩放和 END 拖动；悬停模块显示摘要，双击模块/模块边定位源码，双击章节边查看底层分支明细；支持不可达/到不了 END 角标和选中模块/章节的相关路径高亮；菜单「保存布局」可导出当前布局，但重新打开剧情图会回到自动布局。
- 主题：SimpleTheme 打底，视觉令牌与控件样式由 `Weftline.Widgets` 提供；界面字体使用 Inter，编辑区优先使用 JetBrains Mono。
- Problems 面板已接入；Output/ProjectInfo 面板暂未接入。

### 构建与运行

先构建 native core，再构建 App：

```bash
cmake -S native -B native/out/build/release -G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF
cmake --build native/out/build/release
dotnet build Weftline.slnx
dotnet run --project src/Weftline.App
```

Windows 在 “x64 Native Tools Command Prompt for VS 2022” 中执行同样命令；`Weftline.Native.csproj` 会自动把 `weftline_core.dll` 复制到输出目录。Linux 对应复制 `libweftline_core.so`。

### 测试与基准

```bash
dotnet test Weftline.slnx
dotnet run --project bench/Weftline.Benchmarks -c Release
```

- `tests/Weftline.App.Tests` 覆盖文档诊断合并、Host 集成与语法分析调度。
- `tests/Weftline.Core.Tests` 覆盖剧情图分析与播放器状态机。
- `bench/Weftline.Benchmarks` 覆盖 10KB / 100KB / 1MB 的单文件解析、项目编译和 `TextDocument` 快照。

### 主要 UI 依赖

| 包 | 用途 |
|---|---|
| `Avalonia` / `Avalonia.Desktop` | 桌面 UI 与窗口 |
| `Avalonia.Themes.Simple` | 基础控件主题 |
| `Avalonia.AvaloniaEdit` | 代码编辑器 |
| `CommunityToolkit.Mvvm` | ObservableObject / RelayCommand |
| `Avalonia.Fonts.Inter` | 界面字体 |

## 文档

- [docs/README.md](docs/README.md) — 文档索引与定位
- [docs/language-spec.md](docs/language-spec.md) — 语言语义
- [docs/grammar.md](docs/grammar.md) — 词法与 EBNF
- [docs/examples.md](docs/examples.md) — 示例与走查
- [docs/diagnostics.md](docs/diagnostics.md) — 诊断码与常见错误

## 示例

- [samples/believe.weft](samples/believe.weft)
- [samples/believe.wstory](samples/believe.wstory)

## 定位

Weftline 是**剧情 demo / 叙事演示工具**，不是游戏引擎，也不是商业剧情生产管线。立绘、音频、存档、本地化、语音、RPG 数值/背包、引擎内实时逻辑等能力不在范围内。
