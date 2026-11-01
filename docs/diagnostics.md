# 诊断

[**中文**](./diagnostics.md) | [English](./en/diagnostics.md)


本文定义诊断模型、严重级别与 v0 诊断码。

---

## 1. 诊断模型

```ts
Diagnostic {
  code: string;         // 例如 WFT3004
  severity: "error" | "warning" | "info";
  message: string;
  span: TextSpan;       // 文件、起止偏移
  related?: TextSpan[]; // 关联位置
  quickFix?: QuickFix;  // 可选快速修复
}
```

- `span` 指向源码的精确位置。
- 语法错误尽量恢复并继续解析，以便编辑器仍能补全。
- 项目级诊断（目标解析、可达性）在模块解析完成后统一产生。

---

## 2. 诊断码

### WFT1xxx：词法与语法

| 码 | 级别 | 说明 |
|---|---|---|
| WFT1001 | error | 无法识别的字符或指令 |
| WFT1002 | error | 正文块未闭合 |
| WFT1003 | error | 指令格式错误 |
| WFT1004 | error | 出口分支格式错误 |
| WFT1005 | error | 标题包含非法字符 `{` 或 `}` |

### WFT2xxx：模块与 declarations

| 码 | 级别 | 说明 |
|---|---|---|
| WFT2001 | error | `@var` 出现在 `@globals` 之外 |
| WFT2002 | error | 出现多个 `@globals` 块 |
| WFT2003 | error | `@globals` 不在文件头或独立 globals 文件中 |
| WFT2004 | error | 全局变量重复声明 |
| WFT2005 | error | 模块标题重复 |
| WFT2006 | error | 模块头指令出现在内容之后 |
| WFT2007 | error | `@title` 为空 |
| WFT2008 | error | 对话或出口引用了未声明的角色 |
| WFT2009 | error | 同一模块内角色重复声明 |

### WFT3xxx：内容与出口

| 码 | 级别 | 说明 |
|---|---|---|
| WFT3001 | error | 模块缺少出口块 |
| WFT3002 | error | 出口块缺少兜底（至少一条无条件分支） |
| WFT3003 | error | 出口块混用选择模式与自动模式 |
| WFT3004 | error | 选择模式分支缺少 `[文案]`，或自动模式分支出现 `[文案]` |
| WFT3005 | error | 出口块之后仍有内容 |
| WFT3006 | error | 条件引用了未声明的全局变量 |
| WFT3007 | error | `@set` 引用了未声明的全局变量 |
| WFT3008 | error | 布尔表达式语法错误 |
| WFT3009 | error | 分支效果块中出现非 `@set` 语句 |
| WFT3010 | warning | 同一效果块内重复赋值，后写覆盖 |

### WFT4xxx：项目与图

| 码 | 级别 | 说明 |
|---|---|---|
| WFT4001 | error | `@next` 目标模块未找到 |
| WFT4002 | error | 非法定义或使用 `END` |
| WFT4003 | error | 项目为空，没有可演示的模块 |
| WFT4004 | warning | 模块从默认入口不可达 |
| WFT4005 | warning | 存在无法到达 `END` 的路径 |
| WFT4006 | warning | 单模块隔离模式下存在外部目标 |
| WFT4007 | warning | 图片资源不存在 |
| WFT4008 | info | 全局变量已声明但从未使用 |

---

## 3. 快速修复

| 场景 | 快速修复 |
|---|---|
| 变量未声明 | 在 `@globals` 中添加 `@var`；无 globals 时创建 |
| 目标模块不存在 | 创建目标模块，或把目标改为已有模块 |
| 角色未声明 | 在模块头添加 `@char` |
| 缺少兜底 | 在出口块末尾添加无条件分支 |
| 选择模式缺少文案 | 插入 `[文案]` 占位 |
| 图片不存在 | 打开资源导入 |

---

## 4. 单模块隔离模式的特殊规则

- 外部目标缺失：`WFT4006` 警告，不阻塞演示。
- 外部变量缺失：仍为 `WFT3006` 错误，因为变量必须到唯一 `@globals` 表查找。
- 没有 `@globals`：变量表为空；不使用变量的模块不受影响。
