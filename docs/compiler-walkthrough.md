# Step 1：编译器入门

这份文档配合 Step 1 的代码，目标不是“写完整个编译器”，而是用一个真实但收窄的例子，把下面几件事串起来：

- 源文本如何变成带位置的结构；
- Parser 如何按行消费并构造 AST；
- 诊断如何定位到源码；
- 为什么先做 AST，不急着做完整的语义分析。

Step 1 只处理：`@globals`、模块头、旁白、对话、图片、`@set`，以及把 `@next` 出口块**原文暂存**。exit 的分支、条件、效果留到 Step 2。

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

Weftline 不是表达式密集的语言，正文块又是纯文本，所以 Step 1 没有做全量 Token 流。

Parser 每读到一行，就按当前状态判断它是什么：

- 空行 / 注释
- `@title` / `@char` / `@globals` / `@var` / `@fig` / `@set` / `@next`
- 正文围栏 `[`、`]`
- 对话开头 `角色:[`
- 普通文本（正文块内部）

这是“行级词法 + 递归下降”的组合，适合这种行导向语言。

### 2.3 AST 只保留语义，位置靠 Span

Step 1 的 AST：

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
    ExitSyntax        // 原文占位，Step 2 解析
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
  @next        -> 捕获原文出口块，模块结束
  @char        -> 模块头角色；若在内容之后出现则报错
  @globals/@var-> 不允许
  内容块       -> 旁白 / 对话 / @fig / @set
  其他         -> 诊断
```

### 3.3 正文块

- 单行 `[文本]`、`角色:[文本]` 直接取括号内文本；
- 多行 `[` 换行 … 换行 `]`：只有**单独一行 `]`** 才结束；
- 块内不解析任何关键字，`@title`、`@next` 在正文块里都只是文本。

### 3.4 诊断

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
  WeftlineCompiler.cs         对外入口
src/Weftline.Cli/
  Program.cs                  parse 命令
  AstPrinter.cs               AST 缩进打印
tests/Weftline.Language.Tests/
  ParserTests.cs              解析与诊断测试
samples/step1.weft            可直接用 CLI 运行的示例
```

---

## 5. 运行

```bash
dotnet test
dotnet run --project src/Weftline.Cli -- parse samples/step1.weft
```

成功时会打印 AST；有错误时会打印诊断（文件:行:列、错误码、消息）。

---

## 6. Step 1 刻意不做的事

- exit 分支、条件表达式、`{}` 效果；
- 模块目标解析、变量引用解析；
- 模式/兜底/可达性校验；
- StoryGraph IR 与 HTML 导出。

这些不是“忘了”，而是为了把第一步控制在可读、可学习的范围内。

---

## 7. Step 2 会做什么

1. 把 `ExitSyntax.RawLines` 解析成 `ExitBranchSyntax`；
2. 增加条件表达式 AST 与递归下降解析器；
3. 解析 `{}` 里的 `@set` 效果；
4. Binder：解析目标模块、变量、角色；
5. Validator：模式一致、兜底、可达性；
6. 构建 `StoryGraph`。

---

## 8. 练习建议

按顺序做，每做一项补一个测试：

1. 给 `@char` 增加“角色名重复”诊断（已实现，尝试自己重写）；
2. 给顶层普通文本增加一条更友好的诊断消息；
3. 在 AST 打印器里显示每个节点的行号；
4. 尝试解析 `@next` 的第一行头部（只解析角色与目标）；
5. 为 `@fig` 增加路径转义测试。

---

## 9. 学习提示

- 先跟着一条真实数据走：`samples/step1.weft` → `SourceText` → `Parser` → AST；
- 每增加一个语法结构，先写一个失败测试，再实现；
- 不要一开始就追求完整语法；Step 1 的价值是让“文本 → 结构化数据”这条链路成立；
- 诊断和 AST 是后续一切功能（图、补全、导出）的地基。
