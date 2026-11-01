# Parser Front-End: From Source to AST

[中文](../compiler-walkthrough.md) | **English**

This walkthrough follows the parser front-end: how source text becomes a positioned tree, how diagnostics are produced, and why the AST is separate from the later graph IR.

The parser currently handles `@globals`, module headers, narration, dialogue, figures, `@set`, and `@next` exits with branches, conditions, and effects.

## 1. Data flow

```
string / file
   │
   ▼
SourceText            text + line index + offsets
   │
   ▼
Parser                line state machine + recursive descent
   │
   ├──> ScriptFileSyntax (AST)
   └──> DiagnosticBag
```

Key rule: **the parser does not throw on user errors**. It records diagnostics and keeps going so the editor still has a usable tree.

## 2. Core concepts

### 2.1 SourceText and TextSpan

`SourceText` stores the full text and splits it into lines:

```csharp
public readonly record struct SourceLine(
    string Text,
    TextSpan Span,
    TextSpan SpanIncludingLineBreak,
    int Number);
```

`TextSpan` is a character range:

```csharp
public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
}
```

Offsets are used internally because they make slicing, replacing, and diagnostics simple. Line/column is computed only for display.

### 2.2 Lines as tokens

Weftline is not expression-heavy, and text blocks are opaque, so the parser does not build a full token stream.

Each line is classified by the current parser state:

- blank line / comment
- `@title` / `@char` / `@globals` / `@var` / `@fig` / `@set` / `@next`
- text fence `[`, `]`
- dialogue start `character:[`
- ordinary text inside a text block

This is line-level lexing combined with recursive descent.

### 2.3 AST keeps meaning; spans keep locations

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

`ContentSyntax` is an abstract base and every node carries a `TextSpan`. This supports diagnostics, graph navigation, and future text edits.

## 3. Parsing process

### 3.1 ParseFile

```
skip blank/comment
if @globals -> ParseGlobals
loop:
  skip blank/comment
  @title -> ParseModule
  @globals (duplicate/misplaced) -> diagnostic
  @var (top level) -> diagnostic
  anything else -> "unrecognized top-level line"
```

### 3.2 ParseModule

```
@title       -> module title
loop:
  @title     -> end current module
  @next      -> parse exit block (branches/conditions/effects), end module
  @char      -> header character; error if it appears after content
  @globals/@var -> not allowed
  content    -> narration / dialogue / @fig / @set
  anything else -> diagnostic
```

### 3.3 Text blocks

- Inline `[text]` and `character:[text]` take the inner text directly.
- Multi-line `[` newline ... newline `]`: only a line containing exactly `]` closes it.
- No keywords are parsed inside a text block; `@title` and `@next` are plain text there.

### 3.4 Exit blocks

`@next(character):` opens an exit block; subsequent lines omit `@next`.

- **Condition**: `(condition)` is a boolean expression with `not` > `and` > `or` and parentheses.
- **Label**: `[label]` is only allowed in choice mode.
- **Target**: text after `->` up to `{` or the end of the line; may contain spaces.
- **Effects**: `{}` may contain only `@set`, either inline separated by `;` or on separate lines.
- **Validation**: mode consistency, at least one fallback branch, no content after the exit.

### 3.5 Diagnostics

Every diagnostic carries a `TextSpan`. The CLI converts offsets to line/column:

```csharp
var position = result.Source.GetLinePosition(diagnostic.Span.Start);
```

## 4. Code map

```
src/Weftline.Language/
  Source/TextSpan.cs          source ranges
  Source/SourceText.cs        text, line index, line/column conversion
  Diagnostics/Diagnostic.cs   diagnostics, bag, codes
  Syntax/Ast.cs               AST records
  Syntax/Parser.cs            line-oriented parser
  Syntax/ConditionParser.cs   boolean expression parser
  Syntax/SyntaxFacts.cs       identifier and boolean literal helpers
  WeftlineCompiler.cs         public entry point
src/Weftline.Cli/
  Program.cs                  parse command
  AstPrinter.cs               indented AST printer
tests/Weftline.Language.Tests/
  ParserTests.cs              content parsing and diagnostics
  ExitParserTests.cs          exit branches, conditions, effects
samples/believe.weft          runnable sample
```

## 5. Running

```bash
dotnet test
dotnet run --project src/Weftline.Cli -- parse samples/believe.weft
```

On success the CLI prints the AST; on errors it prints diagnostics with file, line, column, code, and message.

## 6. Current scope

- Module target resolution
- Reachability / `END` validation
- StoryGraph IR and HTML export

These are intentionally outside the parser front-end.

## 7. Next steps

1. Binder: resolve target modules and entry points.
2. Validator: reachability and `END` checks.
3. Build the `StoryGraph` IR.
4. Export JSON.
5. Connect the graph view and HTML player.

## 8. Reading tips

- Follow one real input: `samples/believe.weft` → `SourceText` → `Parser` → AST.
- Add a failing test before adding a new syntax feature.
- Do not try to implement the whole language at once; the current value is that "text → structured data" works end to end.
- Diagnostics and the AST are the foundation for the graph, completion, and export.
