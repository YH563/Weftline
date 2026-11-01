# Weftline Grammar

[中文](../grammar.md) | **English**

This document defines the v0 lexical and syntactic rules. Semantics are in [language-spec.md](./language-spec.md).

## 1. Lexical rules

### 1.1 Lines and whitespace

- The grammar is line-oriented.
- Indentation is cosmetic and has no semantic meaning. The editor auto-indents exit branches.
- Blank lines may appear between structures.
- Trailing whitespace is ignored outside text blocks.

### 1.2 Keywords

All directives start with `@`. There are only seven:

```
@globals
@var
@title
@char
@fig
@set
@next
```

`END` is a reserved module ID, not a keyword.

### 1.3 Identifiers

Used for character and variable names:

```
Identifier := (Unicode Letter | "_") (Unicode Letter | Digit | "_")*
```

Unicode identifiers are allowed, including Chinese names.

### 1.4 Booleans

```
BoolLiteral := "true" | "false"
```

### 1.5 Strings

Used for `@fig` paths. Strings are double-quoted and support:

```
\"   double quote
\\   backslash
```

### 1.6 Comments

```
// comment
```

- Only at the structural level.
- Inside text blocks, `//` is plain text.

### 1.7 Text blocks

```
[ first line
  second line
]
```

- A multi-line block opens with a line containing only `[` and closes with a line containing only `]`.
- Everything inside is plain text; keywords are not parsed.
- Inline form: `[text]`; the first `]` closes it. Escape `\]` if needed.
- Inline dialogue: `character:[text]`.

### 1.8 Titles and targets

- `TitleText`: the rest of the `@title` line, trimmed.
- `TargetText`: the text after `->` up to `{` or the end of the line, trimmed.
- Titles and targets may contain spaces, but not `{` or `}`.

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

## 3. Parsing rules

### 3.1 Module boundaries

- Every `@title` starts a new module.
- A new `@title` ends the previous module.
- EOF ends the last module.
- Before the first `@title`, only `@globals` and comments are allowed.

### 3.2 Module header

- `@char` may appear only after `@title` and before any content or exit block.
- `@char` is not allowed after content has started.

### 3.3 Text blocks

- A line containing only `[` starts a multi-line narration block.
- A line containing only `character:[` starts a multi-line dialogue block.
- A line containing only `]` closes either block.
- Inline `[text]` and `character:[text]` are also accepted.
- Inside a multi-line block, `]` is text unless it is alone on the line.

### 3.4 Exit block

An exit block starts at the first `@next` line and ends at the next `@title` or EOF.

Full form:

```
@next(character):(condition)[label] -> target { @set ... }
    (condition)[label] -> target
    ()[label] -> target
```

Shorthand:

```
@next -> target
```

- In the full form, the colon after `@next(...)` or `@next()` is required.
- Subsequent branch lines omit `@next`.
- The first branch is on the same line as the header.
- `@next -> target` is equivalent to `@next(): -> target`.
- The effect block may be inline or multi-line.
- `@next` must be the last part of a module; only branch lines and comments may follow it.

### 3.5 Conditions

- Conditions are parenthesized boolean expressions.
- Conditions may be omitted.
- Precedence: `not` > `and` > `or`.
- Parentheses group.

### 3.6 Mode constraints

| Header | Mode | Requirement |
|---|---|---|
| `@next(character):` | Choice | Every branch must have `[label]`. |
| `@next():` | Automatic | Branches must not have `[label]`. |
| `@next -> target` | Automatic (single) | No label, no condition. |

Mixing labeled and unlabeled branches in one block is an error.

### 3.7 Fallback

Every exit block must contain at least one **unconditional branch** (no condition, or `()`).

- Choice mode: at least one option is always visible.
- Automatic mode: routing never dead-ends.

## 4. Example

```
@globals
@var trust_low false
@var has_key false

@title believe
@char xiaoying
@char shadow

[
Rain blurred the view.
]

xiaoying:[You came.]

@fig "assets/hospital.png"

@set trust_low true

@next(xiaoying):(trust_low)[Believe him] -> hospital {
    @set has_key true
}
    ()[Stay silent] -> silence

@title hospital
@char xiaoying

@next(): (has_key) -> basement
    () -> corridor

@title basement
@char xiaoying
@next -> END

@title corridor
@char xiaoying
@next -> END

@title silence
@char xiaoying
@next -> END
```
