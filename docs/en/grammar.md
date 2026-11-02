# Weftline Grammar

[中文](../grammar.md) | **English**

This document defines v0 lexing and syntax. See [language-spec.md](./language-spec.md) for semantics.

---

## 1. Lexical conventions

### 1.1 Lines and indentation

- Lines are the basic unit.
- Indentation is display-only: 4 spaces per level, ignored by the parser.
- Blank lines are allowed between structures.
- Trailing whitespace is ignored.
- Leading/trailing whitespace is trimmed from each text-block line.

### 1.2 Keywords

All directives start with `@`:

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

`END` is a reserved terminal identifier, not a keyword.

### 1.3 Identifiers

Used for character, item, scene, and variable names:

```text
Identifier := (Unicode Letter | "_") (Unicode Letter | Digit | "_")*
```

Chinese identifiers such as `萧影` and `医院钥匙` are allowed.

### 1.4 Booleans

```text
BoolLiteral := "true" | "false"
```

### 1.5 Strings

Used for `@fig` paths. Double-quoted, with these escapes:

```text
\"   double quote
\\   backslash
```

### 1.6 Comments

```text
// this is a comment
```

- Only effective at the structural level.
- Inside text blocks, `//` is plain text.

### 1.7 Text blocks and references

```text
[雨水模糊了视线。]
萧影:[你来了。]
```

Multi-line, with brackets attached to text:

```text
[雨水模糊了视线，
他抬起头。]
```

- `{Identifier}` inside a text block references a character or item.
- `{{` means a literal `{`; `}}` means a literal `}`.
- `{}` does not span lines and does not nest.

### 1.8 Titles and target text

- `TitleText`: the rest of a `@title` / `@project` / `@doc` line, trimmed.
- `TargetText`: the text after `->`, before `{` or end of line, trimmed.
- Titles and targets may contain spaces but not `{` or `}`.

---

## 2. EBNF

### 2.1 `.weft` module scripts

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

### 2.2 `.wstory` project overview

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

## 3. Parsing conventions

### 3.1 File roles

- `.weft`: top-level declaration groups + modules.
- `.wstory`: project info + descriptions + grouped declarations.
- `.weft` is the only declaration source.
- `.wstory` declarations are generated; descriptions are hand-written.

### 3.2 Module boundaries

- Each `@title` starts a new module.
- A new `@title` ends the previous module.
- EOF ends the last module.

### 3.3 Module header

```text
@title <title>
@scene <scene>
```

- `@scene` is optional and must appear before content.
- A module has at most one `@scene`.
- Module headers do not declare characters, items, variables, or scenes.

### 3.4 Text blocks

- `[text]` on one line is inline narration.
- `character:[text]` on one line is inline dialogue.
- `[first line` newline `second line]` is a multi-line block; only a `]` inside a line closes it.
- Each line's leading/trailing whitespace is trimmed.
- `{name}` inside a text block is parsed as a reference.

### 3.5 Exit blocks

Full form:

```text
@next(character):(condition)[label] -> target { @set ... }
    (condition)[label] -> target
    ()[label] -> target
```

Shorthand:

```text
@next -> target
@next -> target { @set ... }
```

- In the full form, the colon after `@next(character):` or `@next():` is required.
- Subsequent branch lines omit `@next`; indentation is display-only.
- Shorthand is a single unconditional automatic branch.
- Effect blocks may be inline or multi-line.
- `@set` is only allowed inside branch effects.

### 3.6 Mode constraints

| `@next` header | Mode | Branch requirement |
|---|---|---|
| `@next(character):` | choice | every branch must have `[label]` |
| `@next():` | automatic | branches must not have `[label]` |
| `@next -> target` | automatic (single) | no label, no condition |

Labels and unlabeled branches cannot be mixed in one exit block.

### 3.7 Fallback

Every exit block needs at least one **unconditional branch** (condition omitted or written as `()`).

---

## 4. Complete `.weft` example

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

## 5. Complete `.wstory` example

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
