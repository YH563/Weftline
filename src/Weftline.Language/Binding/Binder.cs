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
    public static void Bind(
        ScriptFileSyntax script,
        DiagnosticBag diagnostics,
        string? filePath = null)
    {
        var path = filePath ?? string.Empty;

        switch (script)
        {
            case WeftFileSyntax weft:
                BindWeft(weft, diagnostics, path);
                break;

            case WStoryFileSyntax wstory:
                BindWStory(wstory, diagnostics, path);
                break;
        }
    }

    private static void BindWeft(
        WeftFileSyntax weft,
        DiagnosticBag diagnostics,
        string filePath)
    {
        var symbols = BuildWeftSymbols(weft, diagnostics, filePath);

        foreach (var module in weft.Modules)
        {
            BindModule(module, symbols, diagnostics);
        }
    }

    private static void BindWStory(
        WStoryFileSyntax wstory,
        DiagnosticBag diagnostics,
        string filePath)
    {
        var symbols = new SymbolTable();
        var chapterTitles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var chapter in wstory.Chapters)
        {
            if (!chapterTitles.Add(chapter.Title))
            {
                diagnostics.Error(
                    DiagnosticCode.DuplicateChapterTitle,
                    chapter.TitleSpan,
                    $"章节标题重复：{chapter.Title}");
            }
        }

        foreach (var variable in wstory.Variables)
        {
            Declare(
                symbols,
                variable.Name,
                SymbolKind.Variable,
                variable.Value,
                variable.Span,
                filePath,
                diagnostics);
        }

        foreach (var character in wstory.Characters)
        {
            Declare(symbols, character.Name, SymbolKind.Character, null, character.Span, filePath, diagnostics);
        }

        foreach (var item in wstory.Items)
        {
            Declare(symbols, item.Name, SymbolKind.Item, null, item.Span, filePath, diagnostics);
        }

        foreach (var scene in wstory.Scenes)
        {
            Declare(symbols, scene.Name, SymbolKind.Scene, null, scene.Span, filePath, diagnostics);
        }
    }

    internal static SymbolTable BuildWeftSymbols(
        WeftFileSyntax weft,
        DiagnosticBag diagnostics,
        string filePath)
    {
        var symbols = new SymbolTable();

        foreach (var variable in weft.Variables)
        {
            Declare(
                symbols,
                variable.Name,
                SymbolKind.Variable,
                variable.Value,
                variable.Span,
                filePath,
                diagnostics);
        }

        foreach (var character in weft.Characters)
        {
            Declare(symbols, character.Name, SymbolKind.Character, null, character.Span, filePath, diagnostics);
        }

        foreach (var item in weft.Items)
        {
            Declare(symbols, item.Name, SymbolKind.Item, null, item.Span, filePath, diagnostics);
        }

        foreach (var scene in weft.Scenes)
        {
            Declare(symbols, scene.Name, SymbolKind.Scene, null, scene.Span, filePath, diagnostics);
        }

        return symbols;
    }

    internal static bool Declare(
        SymbolTable symbols,
        string name,
        SymbolKind kind,
        bool? value,
        TextSpan span,
        string filePath,
        DiagnosticBag diagnostics)
    {
        var entry = new SymbolEntry(name, kind, new SourceLocation(filePath, span), value);

        if (symbols.TryDeclare(entry, out var existing))
        {
            return true;
        }

        var kindName = KindName(kind);

        if (existing!.Kind == kind)
        {
            diagnostics.Error(
                DuplicateCode(kind),
                span,
                $"全局{kindName}重复声明：{name}");
        }
        else
        {
            diagnostics.Error(
                DiagnosticCode.DuplicateSymbol,
                span,
                $"符号名已被声明为{KindName(existing.Kind)}：{name}");
        }

        return false;
    }

    internal static void BindModule(
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

    internal static void ValidateSegments(
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

    internal static void ValidateCondition(
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

    private static string DuplicateCode(SymbolKind kind)
        => kind switch
        {
            SymbolKind.Variable => DiagnosticCode.DuplicateGlobalVariable,
            SymbolKind.Character => DiagnosticCode.DuplicateCharacter,
            SymbolKind.Item => DiagnosticCode.DuplicateItem,
            SymbolKind.Scene => DiagnosticCode.DuplicateScene,
            _ => DiagnosticCode.DuplicateSymbol,
        };

    private static string KindName(SymbolKind kind)
        => kind switch
        {
            SymbolKind.Variable => "变量",
            SymbolKind.Character => "角色",
            SymbolKind.Item => "道具",
            SymbolKind.Scene => "场景",
            _ => "符号",
        };
}
