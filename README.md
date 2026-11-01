# Weftline

剧情 demo / 叙事演示工具：写剧本、看剧情图、导出可在浏览器里直接演示的 HTML。

当前状态：**项目架构与语言文档已就位，尚未开始实现业务代码。**

## 技术栈

- .NET 10 / C#
- Avalonia（跨平台桌面 UI）
- xUnit（测试）

## 解决方案结构

```
Weftline.slnx
src/
  Weftline.Language/        语言核心：解析、诊断、StoryGraph IR（零 UI 依赖）
  Weftline.Export/          StoryGraph -> 单文件 HTML / 资源内嵌
  Weftline.App/             Avalonia UI：编辑器、图视图、资源面板
tests/
  Weftline.Language.Tests/  语言核心单元测试
docs/                       语言与架构文档
```

依赖方向：

```
Weftline.App ──> Weftline.Export ──> Weftline.Language
Weftline.Language.Tests ──> Weftline.Language
```

`Weftline.Language` 不得引用 UI 或导出层；这是保证可测试性和可移植性的边界。

## 构建与测试

```bash
dotnet build
dotnet test
```

## 运行时与兼容性

- 目标框架：`net10.0`（.NET 10 LTS，支持到 2028 年 11 月）。
- 不选 .NET 8 的原因：其 LTS 支持在 2026 年 11 月结束，新项目不应再以它为主目标。
- `global.json` 将 SDK 锁定在 10.0 特性带；`rollForward: latestFeature` 允许同特性带的补丁 SDK。
- 发布采用 self-contained，用户无需安装 .NET 运行时：

```bash
dotnet publish src/Weftline.App -c Release -r win-x64 --self-contained true
dotnet publish src/Weftline.App -c Release -r linux-x64 --self-contained true
dotnet publish src/Weftline.App -c Release -r osx-arm64 --self-contained true
```

- 若以后需要把 `Weftline.Language` 复用到引擎（Unity / Godot），再对它多目标 `net8.0` / `netstandard2.1`。当前不需要，避免过早约束语言核心的 API。

## 文档

- [docs/README.md](docs/README.md) — 文档索引与定位
- [docs/architecture.md](docs/architecture.md) — 项目架构与模块职责
- [docs/grammar.md](docs/grammar.md) — 词法与 EBNF
- [docs/language-spec.md](docs/language-spec.md) — 语言语义
- [docs/ir.md](docs/ir.md) — StoryGraph IR
- [docs/diagnostics.md](docs/diagnostics.md) — 诊断码
- [docs/examples.md](docs/examples.md) — 示例与走查

## 里程碑

1. **M1** 语言核心：Lexer / Parser / IR / 诊断
2. **M2** 图视图：模块、出口、END、入口
3. **M3** HTML 导出与播放器
4. **M4** 编辑器集成：自动缩进、补全、错误定位
5. **M5** 图片导入与打磨
