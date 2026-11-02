# Weftline 语法定义

[**中文**](./grammar.md) | [English](./en/grammar.md)

本文定义 v0 的词法与语法。语义见 [language-spec.md](./language-spec.md)。

---

## 1. 词法约定

### 1.1 行与缩进

- 语法以**行**为基本单位。
- 缩进只用于显示，4 空格一级，不参与解析。
- 空行允许出现在任何结构之间。
- 行尾空白忽略。
- 文本块每行首尾空白会被去掉。

### 1.2 关键字

所有指令均以 `@` 开头：

```text
@project
@summary
@doc
@vars
@var
@chars
@char
@items
@item
@scenes
@scene
@title
@fig
@next
@set
```

`END` 是保留的终点标识，不是关键字。

### 1.3 标识符

用于角色名、道具名、场景名、变量名：

```text
Identifier := (Unicode Letter | "_") (Unicode Letter | Digit | "_")*
```

允许中文标识符，例如 `萧影`、`医院钥匙`。

### 1.4 布尔值

```text
BoolLiteral := "true" | "false"
```

### 1.5 字符串

用于 `@fig` 路径。使用双引号包裹，支持以下转义：

```text
\"   双引号
\\   反斜杠
```

### 1.6 注释

```text
// 这是注释
```

- 仅在**结构层**生效。
- 正文块内部，`//` 是普通文本。

### 1.7 正文块与引用

```text
[雨水模糊了视线。]
萧影:[你来了。]
```

多行时括号贴正文，不单独占行：

```text
[雨水模糊了视线，
他抬起头。]
```

- 正文块内的 `{Identifier}` 是引用，只允许引用角色或道具。
- `{{` 表示字面量 `{`，`}}` 表示字面量 `}`。
- `{}` 不跨行，不支持嵌套。

### 1.8 标题与目标文本

- `TitleText`：`@title` / `@project` / `@doc` 后的整行文本，去除首尾空白。
- `TargetText`：`->` 之后、`{` 之前或行尾的整段文本，去除首尾空白。
- 标题和目标可以包含空格，但不能包含 `{` 或 `}`。

---

## 2. EBNF

### 2.1 `.weft` 模块脚本

```ebnf
WeftFile        ::= DeclarationGroup* Module*

DeclarationGroup ::= VarGroup | CharGroup | ItemGroup | SceneGroup
VarGroup        ::= "@vars" NEWLINE VarDecl+
CharGroup       ::= "@chars" NEWLINE CharDecl+
ItemGroup       ::= "@items" NEWLINE ItemDecl+
SceneGroup      ::= "@scenes" NEWLINE SceneDecl+

VarDecl         ::= "@var" Identifier BoolLiteral NEWLINE
CharDecl        ::= "@char" Identifier NEWLINE
ItemDecl        ::= "@item" Identifier NEWLINE
SceneDecl       ::= "@scene" Identifier NEWLINE

Module          ::= "@title" TitleText NEWLINE
                    ModuleScene?
                    Content*
                    ExitBlock
ModuleScene     ::= "@scene" Identifier NEWLINE

Content         ::= NarrationBlock
                  | DialogueBlock
                  | FigureDirective

NarrationBlock  ::= TextBlock
DialogueBlock   ::= Identifier ":" TextBlock
FigureDirective ::= "@fig" String NEWLINE

TextBlock       ::= InlineText | MultiLineText
InlineText      ::= "[" TextSegment* "]"
MultiLineText   ::= "[" TextSegment* NEWLINE
                    ( TextSegment* NEWLINE )*
                    TextSegment* "]"
TextSegment     ::= Text | Reference
Reference       ::= "{" Identifier "}"

ExitBlock       ::= FullExit | ShortExit
FullExit        ::= "@next" CharGroup? ":" Branch+
ShortExit       ::= "@next" "->" Target EffectBlock? NEWLINE

CharGroup       ::= "(" Identifier? ")"
Branch          ::= Condition? Label? "->" Target EffectBlock? NEWLINE

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

### 2.2 `.wstory` 项目总览

```ebnf
WStoryFile      ::= "@project" TitleText NEWLINE
                    WStoryItem*

WStoryItem      ::= Summary | DocGroup | CharGroup | ItemGroup | SceneGroup | VarGroup

Summary         ::= "@summary" TextBlock NEWLINE

DocGroup        ::= "@docs" NEWLINE DocDecl+
DocDecl         ::= "@doc" TitleText TextBlock NEWLINE

CharGroup       ::= "@chars" NEWLINE WStoryCharDecl+
WStoryCharDecl  ::= "@char" Identifier TextBlock? NEWLINE

ItemGroup       ::= "@items" NEWLINE WStoryItemDecl+
WStoryItemDecl  ::= "@item" Identifier TextBlock? NEWLINE

SceneGroup      ::= "@scenes" NEWLINE WStorySceneDecl+
WStorySceneDecl ::= "@scene" Identifier TextBlock? NEWLINE

VarGroup        ::= "@vars" NEWLINE WStoryVarDecl+
WStoryVarDecl   ::= "@var" Identifier BoolLiteral TextBlock? NEWLINE
```

---

## 3. 解析约定

### 3.1 文件角色

- `.weft`：顶部声明分组 + 模块。
- `.wstory`：项目信息 + 描述 + 分组声明。
- `.weft` 是符号声明的唯一来源。
- `.wstory` 的声明由工具生成；描述手写。

### 3.2 模块边界

- 每个 `@title` 开始一个新模块。
- 出现新的 `@title` 时，上一个模块结束。
- 文件结束（EOF）结束最后一个模块。

### 3.3 模块头

```text
@title <title>
@scene <scene>
```

- `@scene` 可选，只能出现在内容之前。
- 一个模块最多一个 `@scene`。
- 模块头不再声明角色、道具、变量或场景。

### 3.4 正文块

- `[文本]` 在同一行 → 单行正文。
- `角色:[文本]` 在同一行 → 单行对话。
- `[第一行` 换行 `第二行]` → 多行正文；只有出现在行内的 `]` 才结束。
- 多行块每一行首尾空白会被去掉。
- `{名字}` 在正文块内解析为引用。

### 3.5 出口块

完整形式：

```text
@next(角色):(条件)[文案] -> 目标 { @set ... }
    (条件)[文案] -> 目标
    ()[文案] -> 目标
```

简写形式：

```text
@next -> 目标
@next -> 目标 { @set ... }
```

- 完整形式中，`@next(角色):` 或 `@next():` 的冒号是必需的。
- 后续分支行省略 `@next`；缩进仅为显示。
- 简写等价于单一无条件自动分支。
- 效果块可以同行，也可以换行。
- `@set` 只允许出现在分支效果中。

### 3.6 模式约束

| `@next` 头 | 模式 | 分支要求 |
|---|---|---|
| `@next(角色):` | 选择模式 | 每个分支必须有 `[文案]` |
| `@next():` | 自动模式 | 分支不得有 `[文案]` |
| `@next -> 目标` | 自动模式（单分支） | 无文案、无条件 |

同一出口块内不得混用带文案与不带文案的分支。

### 3.7 兜底

每个出口块至少包含一条**无条件分支**（省略条件，或条件写作 `()`）。

---

## 4. 完整 `.weft` 示例

```text
@vars
    @var trust_low false
    @var has_key false

@chars
    @char 萧影
    @char 影子

@items
    @item 医院钥匙

@scenes
    @scene 医院

@title 相信
    @scene 医院
    [雨水模糊了视线，{萧影}在泥泞中奔跑。]
    萧影:[你来了。]
    影子:[别回头。]
    @fig "assets/hospital.png"
@next(萧影):(trust_low)[相信他] -> 医院 { @set has_key true }
    ()[沉默] -> 沉默
```

## 5. 完整 `.wstory` 示例

```text
@project 雨夜
    @summary [一个关于信任的故事。]

    @docs
        @doc 世界规则 [医院在雨夜不关门。]

    @chars
        @char 萧影 [主角。]
        @char 影子 [萧影的旧识。]

    @items
        @item 医院钥匙 [医院地下室的钥匙。]

    @scenes
        @scene 医院 [雨夜里的医院。]

    @vars
        @var trust_low false [萧影是否信任影子。]
        @var has_key false [是否拿到了医院钥匙。]
```
