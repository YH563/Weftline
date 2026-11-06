# Weftline

**中文** | [English](README.en.md)

Weftline 是一个剧情 demo / 叙事演示工具：写剧本、看剧情图、导出可在浏览器里直接演示的 HTML。

当前阶段：语言核心可用；桌面端已具备项目资源管理器、AvaloniaEdit 多标签编辑器、实时项目分析与剧情图面板。编辑 .weft/.wstory 时会在防抖后后台重新编译，并保留部分图；HTML 导出后续在 C++ 侧实现。

## 文件类型

| 后缀 | 角色 |
|---|---|
| `.weft` | 章节脚本：章节标题 + 顶部符号声明 + 若干模块 |
| `.wstory` | 项目总览：项目信息 + 章节 + 从 `.weft` 生成的声明 + 手写描述 |

- 一个 `.weft` 文件就是一个章节。
- `.weft` 是符号声明的唯一来源。
- `.wstory` 不是声明入口；声明由工具生成，描述由作者手写并保留。
- 同一扁平文件夹下所有 `.weft` 属于同一个故事。


## 桌面应用

- 顶部菜单栏 + 可调整宽度的项目资源管理器 + 中央编辑区 + 底部状态栏。
- 项目资源管理器：目录/文件树、懒加载、按后缀显示图标；支持新建文件、新建文件夹、重命名、删除；通过菜单切换项目文件夹。
- 编辑器：AvaloniaEdit 多文件标签，支持关闭标签、Weft 语法高亮、行号、当前行高亮、实线分隔和未保存标记。
- 状态栏：光标行列、选区长度、语言、当前模块/场景、模块/变量数、诊断摘要和运行状态。
- 剧情图面板：实时展示当前项目投影，支持章节/模块分层、入口/出口、拖动、缩放和 END 拖动。
- 主题：SukiUI 浅色主题；界面字体使用 Inter，编辑区优先使用 JetBrains Mono。
- Problems/Output/ProjectInfo 面板暂未接入。

```bash
dotnet run --project src/Weftline.App
```

### 主要 UI 依赖

| 包 | 用途 |
|---|---|
| `Avalonia` / `Avalonia.Desktop` | 桌面 UI 与窗口 |
| `SukiUI` | 主题、窗口样式、通知与弹层 |
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
