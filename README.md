# Weftline

**中文** | [English](README.en.md)

Weftline 是一个剧情 demo / 叙事演示工具：写剧本、看剧情图、导出可在浏览器里直接演示的 HTML。

当前阶段：语言核心已可用，桌面编辑器、图视图和 HTML 导出仍在开发中。

## 文件类型

| 后缀 | 角色 |
|---|---|
| `.weft` | 模块脚本：顶部符号声明 + 模块 |
| `.wstory` | 项目总览：项目信息 + 从 `.weft` 生成的声明 + 手写描述 |

- `.weft` 是符号声明的唯一来源。
- `.wstory` 不是声明入口；声明由工具生成，描述由作者手写并保留。
- 同一扁平文件夹下所有 `.weft` 属于同一个故事。

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
