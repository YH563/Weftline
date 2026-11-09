# Diagnostic Codes and Common Errors

[中文](../diagnostics.md) | **English**

The editor shows a diagnostic code at the problem location. This page lists the codes story authors most often encounter, what they mean, and how to fix them.

---

## 1. Lexing and syntax

| Code | Meaning | Common fix |
|---|---|---|
| WFT1001 | Unrecognized character or directive | Check the `@` directive or put text inside `[]` |
| WFT1002 | Unterminated text block | Add the closing `]` |
| WFT1003 | Malformed directive | Check the arguments, e.g. `@var 信任 false` |
| WFT1004 | Malformed exit branch | Check `(condition)[label] -> target` |
| WFT1005 | Module ID contains `{` or `}` | Remove `{` / `}` from `@module` |
| WFT1006 | Unterminated `{}` reference | Add `}` on the same line |

## 2. Declarations and modules

| Code | Meaning | Common fix |
|---|---|---|
| WFT2001 | Symbol declaration in the wrong place | Declare before the first `@module`; write `@var` / `@char` / `@item` / `@scene` directly or use an optional group |
| WFT2004 | Duplicate global variable | Remove the duplicate `@var` |
| WFT2005 | Duplicate module ID | Rename the duplicate `@module` |
| WFT2006 | Module header directive after content | Move `@scene` before the content |
| WFT2007 | Empty `@title` / `@module` / `@project` / `@chapter` / `@doc` | Provide a title, module ID, or name |
| WFT2008 | Dialogue or exit references an undeclared character | Add `@char` before the first `@module` in `.weft` |
| WFT2009 | Duplicate character | Remove the duplicate `@char` |
| WFT2010 | Duplicate item | Remove the duplicate `@item` |
| WFT2011 | Symbol name clashes across kinds | Give the variable/character/item/scene a unique name |
| WFT2012 | `@set` in `.wstory` | Remove it; variable initial values belong in `.weft` `@var` |
| WFT2013 | `@set` in content | Put variable changes only in exit branch effects `{ @set ... }` |
| WFT2014 | `{name}` references an undeclared character or item | Declare it before the first `@module`, or fix the name |
| WFT2015 | Duplicate scene | Remove the duplicate `@scene` |
| WFT2016 | Invalid scene name | Use a valid identifier such as `医院` |
| WFT2017 | `.weft` is missing a chapter title | Put `@title <chapter>` on the first line |
| WFT2018 | Multiple chapter titles in one file | A `.weft` file has exactly one `@title` |
| WFT2019 | Duplicate chapter title | Give each chapter file a unique title |

## 3. Content and exits

| Code | Meaning | Common fix |
|---|---|---|
| WFT3001 | Module is missing an exit block | Add `@next -> END` or another exit at the end |
| WFT3002 | Exit is missing a fallback | Add an unconditional branch, or write one as `()` |
| WFT3003 | Choice and automatic modes mixed | Choice branches all have `[label]`; automatic branches have none |
| WFT3004 | Label does not match the mode | Add `[label]` in choice mode; remove it in automatic mode |
| WFT3005 | Content after the exit block | Move the content before `@next` |
| WFT3006 | Condition references an undeclared variable | Add `@var` before the first `@module` in `.weft` |
| WFT3007 | `@set` references an undeclared variable | Add `@var` before the first `@module` in `.weft` |
| WFT3008 | Boolean expression syntax error | Check `not` / `and` / `or` and parentheses |
| WFT3009 | Non-`@set` statement in an effect block | Effect blocks only allow `@set` |
| WFT3010 | Repeated assignment in one effect block | Later wins; consider removing the duplicate |

## 4. Project and graph

| Code | Meaning | Common fix |
|---|---|---|
| WFT4001 | Target module not found | Create it or fix the target name |
| WFT4002 | Invalid `END` definition or use | Do not define `@module END`; terminate with `@next -> END` |
| WFT4004 | Module unreachable from the entry | Check whether an exit points to it |
| WFT4005 | A path cannot reach `END` | Check dead ends and fallbacks |
| WFT4007 | Image asset not found | Fix the `@fig` path or import the asset |

---

## 5. Single-module playback

- Missing external targets end the demo or show a stub; they do not block.
- Missing external variables are still errors; declare them in `.weft`.
- With no variable declarations, the variable table is empty; modules that use no variables are unaffected.
