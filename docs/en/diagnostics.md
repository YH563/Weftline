# Diagnostics

[中文](../diagnostics.md) | **English**

This document defines the diagnostic model, severities, and v0 diagnostic codes.

## 1. Diagnostic model

```ts
Diagnostic {
  code: string;         // e.g. WFT3004
  severity: "error" | "warning" | "info";
  message: string;
  span: TextSpan;       // file and character offsets
  related?: TextSpan[]; // related locations
  quickFix?: QuickFix;  // optional quick fix
}
```

- `span` points to an exact source range.
- The parser recovers from syntax errors so the editor can still provide completion and navigation.
- Project-level diagnostics (target resolution, reachability) are produced after module parsing.

## 2. Codes

### WFT1xxx: lexical and syntax

| Code | Severity | Meaning |
|---|---|---|
| WFT1001 | error | Unrecognized character or directive |
| WFT1002 | error | Unterminated text block |
| WFT1003 | error | Malformed directive |
| WFT1004 | error | Malformed exit branch |
| WFT1005 | error | Title contains `{` or `}` |

### WFT2xxx: modules and declarations

| Code | Severity | Meaning |
|---|---|---|
| WFT2001 | error | `@var` appears outside `@globals` |
| WFT2002 | error | Multiple `@globals` blocks |
| WFT2003 | error | `@globals` is not at the file header or in a globals file |
| WFT2004 | error | Duplicate global variable |
| WFT2005 | error | Duplicate module title |
| WFT2006 | error | Header directive after content |
| WFT2007 | error | Empty `@title` |
| WFT2008 | error | Dialogue or exit references an undeclared character |
| WFT2009 | error | Duplicate character in one module |

### WFT3xxx: content and exits

| Code | Severity | Meaning |
|---|---|---|
| WFT3001 | error | Module is missing an exit block |
| WFT3002 | error | Exit block has no unconditional fallback |
| WFT3003 | error | Exit block mixes choice and automatic branches |
| WFT3004 | error | Choice branch lacks a label, or automatic branch has one |
| WFT3005 | error | Content after the exit block |
| WFT3006 | error | Condition references an undeclared variable |
| WFT3007 | error | `@set` references an undeclared variable |
| WFT3008 | error | Boolean expression syntax error |
| WFT3009 | error | Non-`@set` statement inside an effect block |
| WFT3010 | warning | Duplicate assignment in one effect block; last write wins |

### WFT4xxx: project and graph

| Code | Severity | Meaning |
|---|---|---|
| WFT4001 | error | `@next` target module not found |
| WFT4002 | error | Illegal definition or use of `END` |
| WFT4003 | error | Project is empty, no module to preview |
| WFT4004 | warning | Module is unreachable from the default entry |
| WFT4005 | warning | A path cannot reach `END` |
| WFT4006 | warning | External target in isolated single-module mode |
| WFT4007 | warning | Image asset not found |
| WFT4008 | info | Global variable declared but never used |

## 3. Quick fixes

| Situation | Quick fix |
|---|---|
| Undeclared variable | Add `@var` to `@globals`, creating it if needed |
| Target module missing | Create the target module, or change the target |
| Undeclared character | Add `@char` to the module header |
| Missing fallback | Add an unconditional branch at the end |
| Missing choice label | Insert a `[label]` placeholder |
| Missing image | Open the asset importer |

## 4. Isolated single-module mode

- Missing external targets: `WFT4006` warning; does not block preview or export.
- Missing external variables: still `WFT3006`, because variables must resolve against the unique `@globals` table.
- Without `@globals`: the variable table is empty; modules that do not use state are unaffected.
