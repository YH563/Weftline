using Weftline.Language.Diagnostics;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language.Binding;

/// <summary>
/// 在语法树之上做符号解析。
///
/// `.weft` 是符号声明的唯一来源；Binder 合并顶部声明并解析模块中的
/// 正文引用、对话说话人、出口角色、条件变量和分支效果变量。
///
/// `.wstory` 是生成的项目总览；Binder 只检查其中的声明是否重复。
///
/// Symbol resolution on top of the syntax tree.
///
/// `.weft` is the only declaration source. The Binder merges top-level
/// declarations and resolves text references, speakers, exit characters,
/// condition variables, and branch-effect variables.
///
/// `.wstory` is a generated project overview; the Binder only checks duplicate
/// declarations there.
/// </summary>
public static class Binder
{
    public static void Bind(ScriptFileSyntax script, DiagnosticBag diagnostics)
    {
        switch (script)
        {
            case WeftFileSyntax weft:
                BindWeft(weft, diagnostics);
                break;

            case WStoryFileSyntax wstory:
                BuildWStorySymbols(wstory, diagnostics);
                break;
        }
    }

    private static void BindWeft(WeftFileSyntax weft, DiagnosticBag diagnostics)
    {
        var symbols = BuildWeftSymbols(weft, diagnostics);

        foreach (var module in weft.Modules)
        {
            BindModule(module, symbols, diagnostics);
        }
    }

    private static SymbolTable BuildWeftSymbols(WeftFileSyntax weft, DiagnosticBag diagnostics)
    {
        var symbols = new SymbolTable();

        foreach (var variable in weft.Variables)
        {
            if (Declare(symbols, variable.Name, "变量", variable.Span, DiagnosticCode.DuplicateGlobalVariable, diagnostics))
            {
                symbols.Variables.TryAdd(variable.Name, variable.Value);
            }
        }

        foreach (var character in weft.Characters)
        {
            if (Declare(symbols, character.Name, "角色", character.Span, DiagnosticCode.DuplicateCharacter, diagnostics))
            {
                symbols.Characters.TryAdd(character.Name, character.Span);
            }
        }

        foreach (var item in weft.Items)
        {
            if (Declare(symbols, item.Name, "道具", item.Span, DiagnosticCode.DuplicateItem, diagnostics))
            {
                symbols.Items.TryAdd(item.Name, item.Span);
            }
        }

        foreach (var scene in weft.Scenes)
        {
            if (Declare(symbols, scene.Name, "场景", scene.Span, DiagnosticCode.DuplicateScene, diagnostics))
            {
                symbols.Scenes.TryAdd(scene.Name, scene.Span);
            }
        }

        return symbols;
    }

    private static void BuildWStorySymbols(WStoryFileSyntax wstory, DiagnosticBag diagnostics)
    {
        var symbols = new SymbolTable();

        foreach (var variable in wstory.Variables)
        {
            Declare(symbols, variable.Name, "变量", variable.Span, DiagnosticCode.DuplicateGlobalVariable, diagnostics);
        }

        foreach (var character in wstory.Characters)
        {
            Declare(symbols, character.Name, "角色", character.Span, DiagnosticCode.DuplicateCharacter, diagnostics);
        }

        foreach (var item in wstory.Items)
        {
            Declare(symbols, item.Name, "道具", item.Span, DiagnosticCode.DuplicateItem, diagnostics);
        }

        foreach (var scene in wstory.Scenes)
        {
            Declare(symbols, scene.Name, "场景", scene.Span, DiagnosticCode.DuplicateScene, diagnostics);
        }
    }

    private static bool Declare(
        SymbolTable symbols,
        string name,
        string kind,
        TextSpan span,
        string sameKindCode,
        DiagnosticBag diagnostics)
    {
        if (symbols.All.TryGetValue(name, out var existing))
        {
            if (existing.Kind == kind)
            {
                diagnostics.Error(sameKindCode, span, $"全局{kind}重复声明：{name}");
            }
            else
            {
                diagnostics.Error(
                    DiagnosticCode.DuplicateSymbol,
                    span,
                    $"符号名已被声明为{existing.Kind}：{name}");
            }

            return false;
        }

        symbols.All[name] = (kind, span);
        return true;
    }

    private static void BindModule(
        ModuleSyntax module,
        SymbolTable symbols,
        DiagnosticBag diagnostics)
    {
        foreach (var content in module.Content)
        {
            switch (content)
            {
                case DialogueSyntax dialogue:
                    if (!symbols.Characters.ContainsKey(dialogue.Speaker))
                    {
                        diagnostics.Error(
                            DiagnosticCode.UndeclaredCharacter,
                            dialogue.Span,
                            $"对话引用了未声明的角色：{dialogue.Speaker}");
                    }

                    ValidateSegments(dialogue.Segments, symbols, diagnostics);
                    break;

                case NarrationSyntax narration:
                    ValidateSegments(narration.Segments, symbols, diagnostics);
                    break;
            }
        }

        if (module.Exit is { } exit)
        {
            if (exit.Character is { } character &&
                !symbols.Characters.ContainsKey(character))
            {
                diagnostics.Error(
                    DiagnosticCode.UndeclaredCharacter,
                    exit.Span,
                    $"出口引用了未声明的角色：{character}");
            }

            foreach (var branch in exit.Branches)
            {
                if (branch.Condition is { } condition)
                {
                    ValidateCondition(condition, symbols, diagnostics);
                }

                foreach (var effect in branch.Effects)
                {
                    if (!symbols.Variables.ContainsKey(effect.Name))
                    {
                        diagnostics.Error(
                            DiagnosticCode.UndeclaredVariable,
                            effect.Span,
                            $"分支效果引用了未声明变量：{effect.Name}");
                    }
                }
            }
        }
    }

    private static void ValidateSegments(
        IReadOnlyList<TextSegmentSyntax> segments,
        SymbolTable symbols,
        DiagnosticBag diagnostics)
    {
        foreach (var segment in segments)
        {
            if (segment is not TextReferenceSyntax reference)
            {
                continue;
            }

            if (!symbols.Characters.ContainsKey(reference.Name) &&
                !symbols.Items.ContainsKey(reference.Name))
            {
                diagnostics.Error(
                    DiagnosticCode.UndeclaredReference,
                    reference.Span,
                    $"{{{reference.Name}}} 引用了未声明的角色或道具。");
            }
        }
    }

    private static void ValidateCondition(
        BoolExprSyntax expression,
        SymbolTable symbols,
        DiagnosticBag diagnostics)
    {
        switch (expression)
        {
            case BoolRefSyntax reference:
                if (!symbols.Variables.ContainsKey(reference.Name))
                {
                    diagnostics.Error(
                        DiagnosticCode.UndeclaredConditionVariable,
                        reference.Span,
                        $"条件引用了未声明变量：{reference.Name}");
                }

                break;

            case NotExprSyntax not:
                ValidateCondition(not.Operand, symbols, diagnostics);
                break;

            case AndExprSyntax and:
                ValidateCondition(and.Left, symbols, diagnostics);
                ValidateCondition(and.Right, symbols, diagnostics);
                break;

            case OrExprSyntax or:
                ValidateCondition(or.Left, symbols, diagnostics);
                ValidateCondition(or.Right, symbols, diagnostics);
                break;
        }
    }

    private sealed class SymbolTable
    {
        public Dictionary<string, (string Kind, TextSpan Span)> All { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, bool> Variables { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, TextSpan> Characters { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, TextSpan> Items { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, TextSpan> Scenes { get; } = new(StringComparer.Ordinal);
    }
}
