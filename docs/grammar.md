# Weftline 语法定义

[**中文**](./grammar.md) | [English](./en/grammar.md)


本文定义 v0 的词法与语法。语义见 [language-spec.md](./language-spec.md)。

---

## 1. 词法约定

### 1.1 行与空白

- 语法以**行**为基本单位。
- 缩进仅用于可读性，**不参与语义**。编辑器会对出口分支自动缩进。
- 空行允许出现在任何结构之间。
- 除正文块内部外，行尾空白忽略。

### 1.2 关键字

所有指令均以 `@` 开头，且只有以下七个：

```
@globals
@var
@title
@char
@fig
@set
@next
```

`END` 是保留的终点标识，不是关键字。

### 1.3 标识符

用于角色名、变量名：

```
Identifier := (Unicode Letter | "_") (Unicode Letter | Digit | "_")*
```

允许中文标识符，例如 `萧影`、`影子`。

### 1.4 布尔值

```
BoolLiteral := "true" | "false"
```

### 1.5 字符串

用于 `@fig` 路径。使用双引号包裹，支持以下转义：

```
\"   双引号
\\   反斜杠
```

```
String := '"' (Char | '\"' | '\\')* '"'
```

### 1.6 注释

```
// 这是注释
```

- 仅在**结构层**生效。
- 正文块内部，`//` 是普通文本。

### 1.7 正文块

```
[ 第一行
  第二行
]
```

- 多行块以单独一行的 `[` 开始，以单独一行的 `]` 结束。
- 块内所有内容按纯文本处理，不解析任何关键字。
- 单行形式：`[文本]`，遇到第一个 `]` 结束，文本内需用 `\]` 转义。
- 对话单行形式：`角色:[文本]`。

### 1.8 标题与目标文本

- `TitleText`：`@title` 后的整行文本，去除首尾空白。
- `TargetText`：`->` 之后、`{` 之前或行尾的整段文本，去除首尾空白。
- 标题和目标**可以包含空格**，但不能包含 `{` 或 `}`。

---

## 2. EBNF

```ebnf
File            ::= GlobalsBlock? Module* EOF

GlobalsBlock    ::= "@globals" NEWLINE VarDecl+
VarDecl         ::= "@var" Identifier BoolLiteral NEWLINE

Module          ::= "@title" TitleText NEWLINE
                    CharDecl*
                    Content*
                    ExitBlock

CharDecl        ::= "@char" Identifier NEWLINE

Content         ::= NarrationBlock
                  | DialogueBlock
                  | FigureDirective
                  | SetDirective
                  | Comment

NarrationBlock  ::= InlineText | BlockText

DialogueBlock   ::= Identifier ":" (InlineText | BlockText)

InlineText      ::= "[" TextChars "]"
BlockText       ::= "[" NEWLINE RawLine* CloseLine
CloseLine       ::= "]" NEWLINE

FigureDirective ::= "@fig" String NEWLINE
SetDirective    ::= "@set" Identifier BoolLiteral NEWLINE
Comment         ::= "//" ... NEWLINE

ExitBlock       ::= FullExit | ShortExit
FullExit        ::= "@next" CharGroup? ":" BranchBody Branch*
ShortExit       ::= "@next" "->" Target EffectBlock? NEWLINE

CharGroup       ::= "(" Identifier? ")"
Branch          ::= BranchBody
BranchBody      ::= Condition? Label? "->" Target EffectBlock? NEWLINE

Condition       ::= "(" BoolExpr? ")"
Label           ::= "[" LabelText "]"
Target          ::= TargetText | "END"

EffectBlock     ::= "{" SetStmt ( ";" SetStmt )* "}"
                  | "{" NEWLINE SetStmt+ "}"
SetStmt         ::= "@set" Identifier BoolLiteral

BoolExpr        ::= OrExpr
OrExpr          ::= AndExpr ( "or" AndExpr )*
AndExpr         ::= UnaryExpr ( "and" UnaryExpr )*
UnaryExpr       ::= "not" UnaryExpr
                  | "(" BoolExpr ")"
                  | Identifier
                  | BoolLiteral
```

---

## 3. 解析约定

### 3.1 模块边界

- 每个 `@title` 开始一个新模块。
- **出现新的 `@title` 时，上一个模块结束。**
- 文件结束（EOF）结束最后一个模块。
- 文件开头到第一个 `@title` 之间，只允许 `@globals` 和注释。

### 3.2 模块头

```
@title <title>
@char <name>
@char <name>
...
```

- `@char` 只能出现在 `@title` 之后、任何内容块或出口块之前。
- 头部出现 `@char` 之外的指令视为内容/出口，按对应规则解析。
- 头部之后不再允许 `@char`。

### 3.3 正文块

- `[` 单独一行 → 多行正文开始；直到单独一行的 `]` 结束。
- `角色:[` 单独一行 → 多行对话开始；同上。
- `[文本]` 或 `角色:[文本]` 在同一行 → 单行正文/对话。
- 多行块内部的 `]` 只要不是单独一行，都按普通文本处理。

### 3.4 出口块

出口块从第一行 `@next` 开始，直到下一个 `@title` 或 EOF。

**完整形式**：

```
@next(角色):(条件)[文案] -> 目标 { @set ... }
    (条件)[文案] -> 目标
    ()[文案] -> 目标
```

**简写形式**（单一无条件自动分支）：

```
@next -> 目标
```

- 完整形式中，`@next(角色):` 或 `@next():` 的冒号是必需的。
- 后续分支行省略 `@next`。
- 第一个分支与 `@next(...):` 在同一行。
- 简写 `@next -> 目标` 等价于 `@next(): -> 目标`，用于线性续接和终止。
- `EffectBlock` 可以同行，也可以换行。
- `@next` 只能出现在模块末尾；出口块内部只允许分支行与注释。
- `@next` 之后再次出现正文块或 `@set` → 语法错误。

### 3.5 条件

- 条件是括号包裹的布尔表达式：`(trust_low)`、`(a and not b)`。
- 条件可以省略；`()` 等价于无条件。
- 运算符优先级：`not` > `and` > `or`。
- 括号用于显式分组。

### 3.6 模式约束

| `@next` 头 | 模式 | 分支要求 |
|---|---|---|
| `@next(角色):` | 选择模式 | 每个分支必须有 `[文案]` |
| `@next():` | 自动模式 | 分支不得有 `[文案]` |
| `@next -> 目标` | 自动模式（单分支） | 无文案、无条件 |

同一出口块内不得混用带文案与不带文案的分支。

### 3.7 兜底

每个出口块至少包含一条**无条件分支**（省略条件，或条件写作 `()`）。

- 选择模式：保证玩家至少总能看到一个选项。
- 自动模式：保证路由总能落到某处，避免死胡同。

---

## 4. 完整语法示例

```
@globals
@var trust_low false
@var has_key false

@title 相信
@char 萧影
@char 影子

[
雨水模糊了视线……
]

萧影:[你来了。]

@fig "assets/hospital.png"

@set trust_low true

@next(萧影):(trust_low)[相信他] -> 医院 {
    @set has_key true
}
    ()[沉默] -> 沉默

@title 医院
@char 萧影

[
两人走进医院。
]

@next():(has_key) -> 地下层
    () -> 走廊

@title 地下层
@char 萧影

[
萧影找到了钥匙。
]

@next -> END

@title 走廊
@char 萧影

[
走廊空无一人。
]

@next -> END

@title 沉默
@char 萧影

[
萧影没有说话。
]

@next -> END
```
