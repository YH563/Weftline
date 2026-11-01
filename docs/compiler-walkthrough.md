# 解析器前端：从源码到 AST

[**中文**](./compiler-walkthrough.md) | [English](./en/compiler-walkthrough.md)


这份文档配合当前解析器的实现，目标不是“写完整个编译器”，而是用一个真实但收窄的例子，把下面几件事串起来：

- 源文本如何变成带位置的结构；
- Parser 如何按行消费并构造 AST；
- 诊断如何定位到源码；
- 为什么先做 AST，不急着做完整的语义分析。

当前解析器处理：`@globals`、模块头、旁白、对话、图片、`@set`，以及 `@next` 出口块的分支、条件与效果。

---

## 1. 数据流

```
字符串 / 文件
   │
   ▼
SourceText            文本 + 行索引 + 偏移
   │
   ▼
Parser                行状态机 + 递归下降
   │
   ├──> ScriptFileSyntax（AST）
   └──> DiagnosticBag（诊断）
```

关键点：**Parser 不抛异常**。用户写错时，它记录诊断并尽量继续，让编辑器仍然有可用的 AST。

---

## 2. 三个核心概念

### 2.1 SourceText 与 TextSpan

`SourceText` 保存完整文本，并把文本切成行：

```csharp
public readonly record struct SourceLine(
    string Text,
    TextSpan Span,
    TextSpan SpanIncludingLineBreak,
    int Number);
```

`TextSpan` 是字符偏移区间：

```csharp
public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
}
```

为什么不用 `(行, 列)` 作为唯一位置？

- 偏移更容易做字符串切片、替换和诊断；
- 行/列可以从偏移换算出来；
- 后续 GUI 的最小文本补丁也依赖偏移。

### 2.2 把“行”当作 token

Weftline 不是表达式密集的语言，正文块又是纯文本，所以解析器没有做全量 Token 流。

Parser 每读到一行，就按当前状态判断它是什么：

- 空行 / 注释
- `@title` / `@char` / `@globals` / `@var` / `@fig` / `@set` / `@next`
- 正文围栏 `[`、`]`
- 对话开头 `角色:[`
- 普通文本（正文块内部）

这是“行级词法 + 递归下降”的组合，适合这种行导向语言。

### 2.3 AST 只保留语义，位置靠 Span

AST 结构：

```
ScriptFileSyntax
  GlobalsSyntax
    GlobalVariableSyntax
  ModuleSyntax
    CharacterSyntax
    ContentSyntax
      NarrationSyntax
      DialogueSyntax
      FigureSyntax
      SetSyntax
    ExitSyntax
      ExitBranchSyntax[]
        Condition? / Label? / Target / Effects[]
        BoolExprSyntax
        EffectSyntax
```

`ContentSyntax` 是抽象基类，所有子类型都带 `TextSpan`。这样图视图、诊断、后续文本编辑都能定位回源码。

---

## 3. 解析过程

### 3.1 ParseFile

```
跳过空行/注释
如果有 @globals -> ParseGlobals
循环：
  跳过空行/注释
  @title -> ParseModule
  @globals（重复/位置错误）-> 诊断
  @var（顶层）-> 诊断
  其他 -> “无法识别的顶层行”
```

### 3.2 ParseModule

```
@title -> 模块标题
循环：
  @title       -> 当前模块结束
  @next        -> 解析出口块（分支/条件/效果），模块结束
  @char        -> 模块头角色；若在内容之后出现则报错
  @globals/@var-> 不允许
  内容块       -> 旁白 / 对话 / @fig / @set
  其他         -> 诊断
```

### 3.3 正文块

- 单行 `[文本]`、`角色:[文本]` 直接取括号内文本；
- 多行 `[` 换行 … 换行 `]`：只有**单独一行 `]`** 才结束；
- 块内不解析任何关键字，`@title`、`@next` 在正文块里都只是文本。

### 3.4 出口块

`@next(角色):` 打开出口块，后续行省略 `@next`。

- **条件**：`(条件)` 里是布尔表达式，优先级 `not` > `and` > `or`，支持括号。
- **文案**：`[文案]` 只允许在选择模式出现。
- **目标**：`->` 之后到 `{` 或行尾的文本，可以包含空格。
- **效果**：`{}` 内只允许 `@set`，可同行用 `;` 分隔，也可多行。
- **校验**：模式一致、至少一条兜底分支、出口后不能有内容。

### 3.5 诊断

每个诊断都带 `TextSpan`。CLI 会把偏移换算成行列：

```csharp
var position = result.Source.GetLinePosition(diagnostic.Span.Start);
```

---

## 4. 代码地图

```
src/Weftline.Language/
  Source/TextSpan.cs          偏移区间
  Source/SourceText.cs        文本、行索引、行列换算
  Diagnostics/Diagnostic.cs   诊断模型、诊断袋、诊断码
  Syntax/Ast.cs               AST 记录
  Syntax/Parser.cs            行式解析器
  Syntax/ConditionParser.cs   布尔条件表达式解析
  Syntax/SyntaxFacts.cs       标识符与布尔字面量工具
  WeftlineCompiler.cs         对外入口
src/Weftline.Cli/
  Program.cs                  parse 命令
  AstPrinter.cs               AST 缩进打印
tests/Weftline.Language.Tests/
  ParserTests.cs              解析与诊断测试
samples/believe.weft            可直接用 CLI 运行的示例
```

---

## 5. 运行

```bash
dotnet test
dotnet run --project src/Weftline.Cli -- parse samples/believe.weft
```

成功时会打印 AST；有错误时会打印诊断（文件:行:列、错误码、消息）。

---

## 6. 当前范围之外

- 模块目标解析；
- 可达性/终点校验；
- StoryGraph IR 与 HTML 导出。

这些不是遗漏，而是当前解析器的边界。

---

## 7. 后续会补充什么

1. Binder：解析目标模块与入口；
2. Validator：可达性、`END` 校验；
3. 构建 `StoryGraph` IR；
4. 导出 JSON；
5. 接入图视图与 HTML 播放器。

---

## 8. 练习建议

按顺序做，每做一项补一个测试：

1. 给 `@char` 增加“角色名重复”诊断（已实现，尝试自己重写）；
2. 给顶层普通文本增加一条更友好的诊断消息；
3. 在 AST 打印器里显示每个节点的行号；
4. 为条件解析增加一个括号优先级测试；
5. 为 `@fig` 增加路径转义测试。

---

## 9. 阅读提示

- 先跟着一条真实数据走：`samples/believe.weft` → `SourceText` → `Parser` → AST；
- 每增加一个语法结构，先写一个失败测试，再实现；
- 不要一开始就追求完整语法；当前实现的价值是让“文本 → 结构化数据”这条链路成立；
- 诊断和 AST 是后续一切功能（图、补全、导出）的地基。
