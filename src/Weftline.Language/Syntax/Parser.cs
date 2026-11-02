using Weftline.Language.Diagnostics;
using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// Weftline 的面向行递归下降解析器。
///
/// 解析器不构建完整的 token 流。每一行是一个行级单元：空行、注释、指令、
/// 文本围栏或普通行。当前解析器状态决定如何消费该行。
///
/// Line-oriented recursive-descent parser for Weftline.
///
/// The parser does not build a full token stream. Instead, each line is a
/// line-level unit: blank line, comment, directive, text fence, or ordinary
/// line. The current parser state decides how that line is consumed.
/// </summary>
public sealed class Parser
{
    private readonly SourceText _source;
    private readonly DiagnosticBag _diagnostics;
    private readonly HashSet<string> _globalVariables = new(StringComparer.Ordinal);
    private readonly HashSet<string> _moduleTitles = new(StringComparer.Ordinal);
    private int _index;

    private Parser(SourceText source, DiagnosticBag diagnostics)
    {
        _source = source;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// 解析整个源码文本并返回语法树。
    /// Parses the entire source text and returns the syntax tree.
    /// </summary>
    public static ScriptFileSyntax Parse(SourceText source, DiagnosticBag diagnostics)
        => new Parser(source, diagnostics).ParseFile();

    private SourceLine? Current => _index < _source.Lines.Count ? _source.Lines[_index] : null;

    private bool IsAtEnd => _index >= _source.Lines.Count;

    private string CurrentTrimmed => Current?.Text.Trim() ?? string.Empty;

    private ScriptFileSyntax ParseFile()
    {
        GlobalsSyntax? globals = null;
        var modules = new List<ModuleSyntax>();

        SkipBlankAndComments();

        if (Current is { } first && IsDirective(first.Text.Trim(), "@globals"))
        {
            globals = ParseGlobals();
            SkipBlankAndComments();
        }

        while (!IsAtEnd)
        {
            SkipBlankAndComments();
            if (IsAtEnd)
            {
                break;
            }

            var line = Current!.Value;
            var trimmed = line.Text.Trim();

            if (IsDirective(trimmed, "@title"))
            {
                modules.Add(ParseModule());
            }
            else if (IsDirective(trimmed, "@globals"))
            {
                var code = globals is null
                    ? DiagnosticCode.GlobalsNotAtFileHeader
                    : DiagnosticCode.MultipleGlobals;

                _diagnostics.Error(code, line.Span, "@globals 必须唯一，并且只能出现在文件最顶部。");
                ParseGlobals();
            }
            else if (IsDirective(trimmed, "@var"))
            {
                _diagnostics.Error(
                    DiagnosticCode.VarOutsideGlobals,
                    line.Span,
                    "@var 只能出现在 @globals 块内。");
                Advance();
            }
            else
            {
                _diagnostics.Error(
                    DiagnosticCode.UnknownLine,
                    line.Span,
                    "无法识别的顶层行；模块必须以 @title 开始。");
                Advance();
            }

            SkipBlankAndComments();
        }

        return new ScriptFileSyntax(globals, modules, new TextSpan(0, _source.Length));
    }

    private GlobalsSyntax ParseGlobals()
    {
        var startLine = Current!.Value;

        if (!string.Equals(startLine.Text.Trim(), "@globals", StringComparison.Ordinal))
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                startLine.Span,
                "@globals 后面不能再写其他内容。");
        }

        Advance();

        var variables = new List<GlobalVariableSyntax>();
        var end = startLine.Span.End;

        while (true)
        {
            SkipBlankAndComments();
            if (IsAtEnd || !IsDirective(CurrentTrimmed, "@var"))
            {
                break;
            }

            var line = Current!.Value;
            Advance();
            end = line.Span.End;

            var rest = line.Text.Trim()["@var".Length..].Trim();
            var parts = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2 || !SyntaxFacts.IsIdentifier(parts[0]) || !SyntaxFacts.TryParseBoolean(parts[1], out var value))
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    line.Span,
                    "@var 的语法是：@var <布尔变量名> true|false");
                continue;
            }

            var name = parts[0];
            var nameSpan = SpanAfter(line, "@var", name);

            if (!_globalVariables.Add(name))
            {
                _diagnostics.Error(
                    DiagnosticCode.DuplicateGlobalVariable,
                    nameSpan,
                    $"全局变量重复声明：{name}");
                continue;
            }

            variables.Add(new GlobalVariableSyntax(name, value, line.Span));
        }

        if (variables.Count == 0)
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                startLine.Span,
                "@globals 至少需要声明一个 @var。");
        }

        return new GlobalsSyntax(
            variables,
            TextSpan.FromBounds(startLine.Span.Start, end));
    }

    private ModuleSyntax ParseModule()
    {
        var titleLine = Current!.Value;
        Advance();

        var trimmedTitleLine = titleLine.Text.Trim();
        var title = trimmedTitleLine.Length > "@title".Length
            ? trimmedTitleLine["@title".Length..].Trim()
            : string.Empty;

        if (title.Length == 0)
        {
            _diagnostics.Error(DiagnosticCode.EmptyTitle, titleLine.Span, "@title 不能为空。");
        }
        else if (!_moduleTitles.Add(title))
        {
            _diagnostics.Error(
                DiagnosticCode.DuplicateModuleTitle,
                titleLine.Span,
                $"模块标题重复：{title}");
        }

        var titleSpan = title.Length == 0
            ? titleLine.Span
            : SpanAfter(titleLine, "@title", title);

        var characters = new List<CharacterSyntax>();
        var characterNames = new HashSet<string>(StringComparer.Ordinal);
        var content = new List<ContentSyntax>();
        var seenContent = false;
        var moduleEnd = titleLine.Span.End;
        ExitSyntax? exit = null;

        while (!IsAtEnd)
        {
            SkipBlankAndComments();
            if (IsAtEnd)
            {
                break;
            }

            var line = Current!.Value;
            var trimmed = line.Text.Trim();

            if (IsDirective(trimmed, "@title"))
            {
                break;
            }

            if (IsDirective(trimmed, "@next"))
            {
                exit = ParseExit();
                moduleEnd = exit.Span.End;
                break;
            }

            if (IsDirective(trimmed, "@char"))
            {
                if (seenContent)
                {
                    _diagnostics.Error(
                        DiagnosticCode.HeaderAfterContent,
                        line.Span,
                        "@char 只能出现在模块内容之前。");
                }

                Advance();
                moduleEnd = line.Span.End;

                var name = trimmed["@char".Length..].Trim();
                if (!SyntaxFacts.IsIdentifier(name))
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedDirective,
                        line.Span,
                        "@char 的语法是：@char <角色名>");
                    continue;
                }

                if (!characterNames.Add(name))
                {
                    _diagnostics.Error(
                        DiagnosticCode.DuplicateCharacter,
                        line.Span,
                        $"角色重复声明：{name}");
                    continue;
                }

                characters.Add(new CharacterSyntax(name, line.Span));
                continue;
            }

            if (IsDirective(trimmed, "@globals") || IsDirective(trimmed, "@var"))
            {
                _diagnostics.Error(
                    DiagnosticCode.VarOutsideGlobals,
                    line.Span,
                    "@globals / @var 不能出现在模块内。");
                Advance();
                moduleEnd = line.Span.End;
                continue;
            }

            seenContent = true;
            ContentSyntax? parsed = null;

            if (trimmed == "[")
            {
                parsed = ParseMultilineNarration();
            }
            else if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
                     trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                var text = trimmed[1..^1].Trim();
                parsed = new NarrationSyntax(text, line.Span);
                Advance();
            }
            else if (IsDirective(trimmed, "@fig"))
            {
                parsed = ParseFigure();
            }
            else if (IsDirective(trimmed, "@set"))
            {
                parsed = ParseSet();
            }
            else if (TryGetDialoguePrefix(line.Text, out var speaker, out var rest))
            {
                if (!characterNames.Contains(speaker))
                {
                    _diagnostics.Error(
                        DiagnosticCode.UndeclaredCharacter,
                        line.Span,
                        $"对话引用了未在本模块声明的角色：{speaker}");
                }

                parsed = ParseDialogue(line, speaker, rest);
            }
            else
            {
                _diagnostics.Error(
                    DiagnosticCode.UnknownLine,
                    line.Span,
                    "无法识别的行；旁白和对话必须写在 [] 中。");
                Advance();
            }

            if (parsed is not null)
            {
                content.Add(parsed);
                moduleEnd = Math.Max(moduleEnd, parsed.Span.End);
            }
        }

        if (exit is null)
        {
            _diagnostics.Error(
                DiagnosticCode.MissingExitBlock,
                titleLine.Span,
                $"模块“{title}”缺少 @next 出口块。");
        }

        return new ModuleSyntax(
            title,
            titleSpan,
            characters,
            content,
            exit,
            TextSpan.FromBounds(titleLine.Span.Start, moduleEnd));
    }

    /// <summary>
    /// 解析 @next 出口块。支持完整形式和
    /// @next -> 目标 的简写形式。
    /// Parses an @next exit block. Supports the full form and the
    /// @next -> target shorthand.
    /// </summary>
    private ExitSyntax ParseExit()
    {
        var startLine = Current!.Value;
        var text = startLine.Text;
        var afterNext = text.IndexOf("@next", StringComparison.Ordinal) + "@next".Length;
        var cursor = afterNext;

        SkipWhitespace(text, ref cursor);

        if (StartsWithAt(text, cursor, "->"))
        {
            var branch = ParseBranch(startLine, cursor, shorthand: true);
            var shorthandBranches = branch is null
                ? new List<ExitBranchSyntax>()
                : new List<ExitBranchSyntax> { branch };

            ConsumeAfterShorthand();
            ValidateExit(null, shorthandBranches, startLine);

            return new ExitSyntax(
                null,
                shorthandBranches,
                TextSpan.FromBounds(startLine.Span.Start, branch?.Span.End ?? startLine.Span.End));
        }

        string? character = null;

        if (cursor < text.Length && text[cursor] == '(')
        {
            var close = FindMatchingParen(text, cursor);
            if (close < 0)
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    startLine.Span,
                    "@next 的角色括号没有闭合。");

                ConsumeToNextModuleOrEnd();
                ValidateExit(null, new List<ExitBranchSyntax>(), startLine);
                return new ExitSyntax(null, Array.Empty<ExitBranchSyntax>(), startLine.Span);
            }

            var inner = text[(cursor + 1)..close].Trim();
            if (inner.Length > 0)
            {
                if (!SyntaxFacts.IsIdentifier(inner))
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedDirective,
                        startLine.Span,
                        "@next 的角色名不合法。");
                }
                else
                {
                    character = inner;
                }
            }

            cursor = close + 1;
            SkipWhitespace(text, ref cursor);
        }

        if (cursor >= text.Length || text[cursor] != ':')
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                startLine.Span,
                "@next 的头部应为：@next(角色): 或 @next():");

            var recovered = ParseBranch(startLine, cursor, shorthand: false);
            var recoveredBranches = recovered is null
                ? new List<ExitBranchSyntax>()
                : new List<ExitBranchSyntax> { recovered };

            ConsumeToNextModuleOrEnd();
            ValidateExit(character, recoveredBranches, startLine);

            return new ExitSyntax(
                character,
                recoveredBranches,
                TextSpan.FromBounds(startLine.Span.Start, recovered?.Span.End ?? startLine.Span.End));
        }

        cursor++;

        var branches = new List<ExitBranchSyntax>();
        var firstBranch = ParseBranch(startLine, cursor, shorthand: false);
        if (firstBranch is not null)
        {
            branches.Add(firstBranch);
        }

        var end = firstBranch?.Span.End ?? startLine.Span.End;

        while (!IsAtEnd)
        {
            var line = Current!.Value;
            var trimmed = line.Text.Trim();

            if (IsDirective(trimmed, "@title"))
            {
                break;
            }

            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                Advance();
                continue;
            }

            if (!IsBranchStart(trimmed))
            {
                _diagnostics.Error(
                    DiagnosticCode.ContentAfterExit,
                    line.Span,
                    "出口块之后不能再有内容。");
                ConsumeToNextModuleOrEnd();
                break;
            }

            var branch = ParseBranch(line, 0, shorthand: false);
            if (branch is null)
            {
                ConsumeToNextModuleOrEnd();
                break;
            }

            branches.Add(branch);
            end = branch.Span.End;
        }

        ValidateExit(character, branches, startLine);
        return new ExitSyntax(character, branches, TextSpan.FromBounds(startLine.Span.Start, end));
    }

    /// <summary>
    /// 解析一条出口分支。返回前会消费当前行（以及可能的多行效果块）。
    /// Parses one exit branch. Consumes the current line (and any multi-line
    /// effect block) before returning.
    /// </summary>
    private ExitBranchSyntax? ParseBranch(SourceLine line, int contentStart, bool shorthand)
    {
        var text = line.Text;
        var position = contentStart;
        SkipWhitespace(text, ref position);
        var branchStart = position;

        if (position >= text.Length)
        {
            _diagnostics.Error(DiagnosticCode.MalformedExitBranch, line.Span, "出口分支不能为空。");
            Advance();
            return null;
        }

        BoolExprSyntax? condition = null;
        TextSpan? conditionSpan = null;

        if (!shorthand && text[position] == '(')
        {
            var close = FindMatchingParen(text, position);
            if (close < 0)
            {
                _diagnostics.Error(DiagnosticCode.InvalidCondition, line.Span, "条件括号没有闭合。");
                Advance();
                return null;
            }

            var innerStart = position + 1;
            var inner = text[innerStart..close];
            conditionSpan = TextSpan.FromBounds(line.Span.Start + position, line.Span.Start + close + 1);

            if (!string.IsNullOrWhiteSpace(inner))
            {
                condition = ConditionParser.Parse(inner, line.Span.Start + innerStart, _diagnostics);
                if (condition is not null)
                {
                    ValidateConditionVariables(condition);
                }
            }

            position = close + 1;
            SkipWhitespace(text, ref position);
        }

        string? label = null;
        TextSpan? labelSpan = null;

        if (!shorthand && position < text.Length && text[position] == '[')
        {
            var close = text.IndexOf(']', position + 1);
            if (close < 0)
            {
                _diagnostics.Error(DiagnosticCode.MalformedExitBranch, line.Span, "选项文案缺少右方括号。");
                Advance();
                return null;
            }

            var raw = text[(position + 1)..close];
            var leading = raw.Length - raw.TrimStart().Length;
            label = raw.Trim();
            labelSpan = label.Length == 0
                ? TextSpan.Empty(line.Span.Start + position + 1)
                : new TextSpan(line.Span.Start + position + 1 + leading, label.Length);

            position = close + 1;
            SkipWhitespace(text, ref position);
        }

        if (!StartsWithAt(text, position, "->"))
        {
            _diagnostics.Error(DiagnosticCode.MalformedExitBranch, line.Span, "出口分支缺少 ->。");
            Advance();
            return null;
        }

        position += 2;
        SkipWhitespace(text, ref position);

        var targetEnd = text.IndexOf('{', position);
        if (targetEnd < 0)
        {
            targetEnd = text.Length;
        }

        var rawTarget = text[position..targetEnd];
        var targetLeading = rawTarget.Length - rawTarget.TrimStart().Length;
        var target = rawTarget.Trim();

        if (target.Length == 0)
        {
            _diagnostics.Error(DiagnosticCode.MalformedExitBranch, line.Span, "出口分支缺少目标模块。");
            Advance();
            return null;
        }

        var targetSpan = new TextSpan(line.Span.Start + position + targetLeading, target.Length);
        position = targetEnd;

        var effects = new List<EffectSyntax>();
        TextSpan? effectSpan = null;
        var advanceAtEnd = true;

        if (position < text.Length && text[position] == '{')
        {
            var effectStart = position;
            var close = text.IndexOf('}', position + 1);

            if (close >= 0)
            {
                ParseEffectStatements(text, position + 1, close, line, effects);
                effectSpan = TextSpan.FromBounds(
                    line.Span.Start + effectStart,
                    line.Span.Start + close + 1);

                if (text[(close + 1)..].Trim().Length > 0)
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedExitBranch,
                        line.Span,
                        "效果块之后不能再有内容。");
                }
            }
            else
            {
                if (text[(position + 1)..].Trim().Length > 0)
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedExitBranch,
                        line.Span,
                        "多行效果块的 { 后不能有内容。");
                }

                Advance();
                advanceAtEnd = false;

                var effectEnd = line.Span.End;
                var closed = false;

                while (!IsAtEnd)
                {
                    var effectLine = Current!.Value;
                    var trimmed = effectLine.Text.Trim();

                    if (trimmed == "}")
                    {
                        closed = true;
                        effectEnd = effectLine.Span.End;
                        Advance();
                        break;
                    }

                    if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        Advance();
                        continue;
                    }

                    ParseEffectStatements(effectLine.Text, 0, effectLine.Text.Length, effectLine, effects);
                    effectEnd = effectLine.Span.End;
                    Advance();
                }

                if (!closed)
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedExitBranch,
                        line.Span,
                        "效果块缺少闭合的 }。");
                }

                effectSpan = TextSpan.FromBounds(line.Span.Start + effectStart, effectEnd);
            }
        }

        if (advanceAtEnd)
        {
            Advance();
        }

        var branchEnd = effectSpan?.End ?? line.Span.End;
        var branchSpan = TextSpan.FromBounds(line.Span.Start + branchStart, branchEnd);

        return new ExitBranchSyntax(
            condition,
            label,
            target,
            effects,
            branchSpan,
            targetSpan,
            conditionSpan,
            labelSpan,
            effectSpan);
    }

    private void ParseEffectStatements(
        string text,
        int start,
        int end,
        SourceLine line,
        List<EffectSyntax> effects)
    {
        var position = start;

        while (position < end)
        {
            while (position < end && (char.IsWhiteSpace(text[position]) || text[position] == ';'))
            {
                position++;
            }

            if (position >= end)
            {
                break;
            }

            var statementStart = position;
            var nextSemicolon = text.IndexOf(';', position);
            var statementEnd = nextSemicolon >= 0 && nextSemicolon < end ? nextSemicolon : end;

            var raw = text[statementStart..statementEnd];
            var leading = raw.Length - raw.TrimStart().Length;
            var statement = raw.Trim();
            var absoluteStart = line.Span.Start + statementStart + leading;

            var isSet = statement.StartsWith("@set", StringComparison.Ordinal)
                && (statement.Length == 4 || char.IsWhiteSpace(statement[4]));

            if (!isSet)
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedEffect,
                    line.Span,
                    "效果块内只允许 @set 语句。");
            }
            else
            {
                var rest = statement["@set".Length..].Trim();
                var parts = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2 ||
                    !SyntaxFacts.IsIdentifier(parts[0]) ||
                    !SyntaxFacts.TryParseBoolean(parts[1], out var value))
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedEffect,
                        line.Span,
                        "@set 的语法是：@set <布尔变量名> true|false");
                }
                else
                {
                    var name = parts[0];
                    if (!_globalVariables.Contains(name))
                    {
                        _diagnostics.Error(
                            DiagnosticCode.UndeclaredVariable,
                            line.Span,
                            $"使用了未在 @globals 中声明的变量：{name}");
                    }

                    var effect = new EffectSyntax(
                        name,
                        value,
                        new TextSpan(absoluteStart, statement.Length));

                    if (effects.Any(existing => existing.Name == name))
                    {
                        _diagnostics.Warning(
                            DiagnosticCode.DuplicateEffectAssignment,
                            effect.Span,
                            $"同一效果块内重复赋值：{name}");
                    }

                    effects.Add(effect);
                }
            }

            position = statementEnd + 1;
        }
    }

    private void ValidateConditionVariables(BoolExprSyntax expression)
    {
        switch (expression)
        {
            case BoolRefSyntax reference:
                if (!_globalVariables.Contains(reference.Name))
                {
                    _diagnostics.Error(
                        DiagnosticCode.UndeclaredConditionVariable,
                        reference.Span,
                        $"条件引用了未在 @globals 中声明的变量：{reference.Name}");
                }
                break;

            case NotExprSyntax not:
                ValidateConditionVariables(not.Operand);
                break;

            case AndExprSyntax and:
                ValidateConditionVariables(and.Left);
                ValidateConditionVariables(and.Right);
                break;

            case OrExprSyntax or:
                ValidateConditionVariables(or.Left);
                ValidateConditionVariables(or.Right);
                break;
        }
    }

    private void ValidateExit(string? character, List<ExitBranchSyntax> branches, SourceLine startLine)
    {
        if (branches.Count == 0)
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedExitBranch,
                startLine.Span,
                "出口块至少需要一条分支。");
            return;
        }

        if (!branches.Any(static branch => branch.Condition is null))
        {
            _diagnostics.Error(
                DiagnosticCode.MissingFallback,
                startLine.Span,
                "出口块缺少无条件兜底分支。");
        }

        var choiceMode = character is not null;
        var hasLabel = branches.Any(static branch => branch.LabelSpan is not null);
        var hasUnlabeled = branches.Any(static branch => branch.LabelSpan is null);

        if (choiceMode)
        {
            foreach (var branch in branches.Where(static branch => branch.LabelSpan is null || string.IsNullOrEmpty(branch.Label)))
            {
                _diagnostics.Error(
                    DiagnosticCode.InvalidBranchLabel,
                    branch.Span,
                    "选择模式的分支必须带非空 [文案]。");
            }
        }
        else
        {
            foreach (var branch in branches.Where(static branch => branch.LabelSpan is not null))
            {
                _diagnostics.Error(
                    DiagnosticCode.InvalidBranchLabel,
                    branch.Span,
                    "自动模式的分支不能带 [文案]。");
            }
        }

        if (hasLabel && hasUnlabeled)
        {
            _diagnostics.Error(
                DiagnosticCode.ModeMismatch,
                startLine.Span,
                "出口块不能混用带文案和不带文案的分支。");
        }
    }

    private void ConsumeAfterShorthand()
    {
        var reported = false;

        while (!IsAtEnd)
        {
            var line = Current!.Value;
            var trimmed = line.Text.Trim();

            if (IsDirective(trimmed, "@title"))
            {
                break;
            }

            if (!reported && trimmed.Length > 0 && !trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                _diagnostics.Error(
                    DiagnosticCode.ContentAfterExit,
                    line.Span,
                    "简写出口只允许一条分支；后续内容无效。");
                reported = true;
            }

            Advance();
        }
    }

    private void ConsumeToNextModuleOrEnd()
    {
        while (!IsAtEnd)
        {
            if (IsDirective(Current!.Value.Text.Trim(), "@title"))
            {
                break;
            }

            Advance();
        }
    }

    private NarrationSyntax ParseMultilineNarration()
    {
        var openLine = Current!.Value;
        var text = ParseMultilineText(openLine, out var end);
        return new NarrationSyntax(text, TextSpan.FromBounds(openLine.Span.Start, end));
    }

    private DialogueSyntax ParseDialogue(SourceLine openingLine, string speaker, string rest)
    {
        if (rest == "[")
        {
            var text = ParseMultilineText(openingLine, out var end);
            return new DialogueSyntax(
                speaker,
                text,
                TextSpan.FromBounds(openingLine.Span.Start, end));
        }

        if (rest.Length >= 2 && rest[0] == '[' && rest[^1] == ']')
        {
            var text = rest[1..^1].Trim();
            Advance();
            return new DialogueSyntax(speaker, text, openingLine.Span);
        }

        _diagnostics.Error(
            DiagnosticCode.MalformedDirective,
            openingLine.Span,
            "对话格式应为：角色:[文本] 或 角色:[ 换行 ... ]");
        Advance();
        return new DialogueSyntax(speaker, string.Empty, openingLine.Span);
    }

    /// <summary>
    /// 消费一个多行文本块。进入时 Current 指向起始的 [ 或 character:[ 行。
    /// 所有内容都按纯文本处理；只有仅包含 ] 的行才会关闭文本块。
    /// Consumes a multi-line text block. On entry, Current points at the opening
    /// [ or character:[ line. All content is treated as plain text; only a line
    /// consisting solely of ] closes the block.
    /// </summary>
    private string ParseMultilineText(SourceLine openingLine, out int end)
    {
        Advance();

        var lines = new List<string>();
        var closed = false;

        while (!IsAtEnd)
        {
            var line = Current!.Value;
            if (line.Text.Trim() == "]")
            {
                closed = true;
                Advance();
                break;
            }

            lines.Add(line.Text);
            Advance();
        }

        if (!closed)
        {
            _diagnostics.Error(
                DiagnosticCode.UnterminatedTextBlock,
                openingLine.Span,
                "文本块缺少单独一行的 ] 作为结束。");
            end = LastLineEnd();
        }
        else
        {
            end = _source.Lines[_index - 1].Span.End;
        }

        return string.Join("\n", lines);
    }

    private FigureSyntax? ParseFigure()
    {
        var line = Current!.Value;
        Advance();

        var rest = line.Text.Trim()["@fig".Length..].Trim();
        if (rest.Length < 2 || rest[0] != '"' || rest[^1] != '"')
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                line.Span,
                "@fig 的语法是：@fig \"路径\"");
            return null;
        }

        var path = Unescape(rest[1..^1]);
        return new FigureSyntax(path, line.Span);
    }

    private SetSyntax? ParseSet()
    {
        var line = Current!.Value;
        Advance();

        var rest = line.Text.Trim()["@set".Length..].Trim();
        var parts = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2 || !SyntaxFacts.IsIdentifier(parts[0]) || !SyntaxFacts.TryParseBoolean(parts[1], out var value))
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                line.Span,
                "@set 的语法是：@set <布尔变量名> true|false");
            return null;
        }

        if (!_globalVariables.Contains(parts[0]))
        {
            _diagnostics.Error(
                DiagnosticCode.UndeclaredVariable,
                line.Span,
                $"使用了未在 @globals 中声明的变量：{parts[0]}");
        }

        return new SetSyntax(parts[0], value, line.Span);
    }

    private void SkipBlankAndComments()
    {
        while (!IsAtEnd)
        {
            var trimmed = CurrentTrimmed;
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                Advance();
                continue;
            }

            break;
        }
    }

    private void Advance() => _index++;

    private int LastLineEnd() => _source.Lines.Count == 0 ? 0 : _source.Lines[^1].Span.End;

    private static bool IsDirective(string trimmed, string keyword)
    {
        if (!trimmed.StartsWith(keyword, StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Length == keyword.Length)
        {
            return true;
        }

        var next = trimmed[keyword.Length];
        return char.IsWhiteSpace(next) || next is '(' or ':';
    }

    private static bool IsBranchStart(string trimmed)
    {
        if (trimmed.StartsWith("->", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.StartsWith("(", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return trimmed.Contains("->", StringComparison.Ordinal);
        }

        return false;
    }

    private static bool TryGetDialoguePrefix(string text, out string speaker, out string rest)
    {
        speaker = string.Empty;
        rest = string.Empty;

        var colon = text.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        var candidate = text[..colon].Trim();
        if (!SyntaxFacts.IsIdentifier(candidate))
        {
            return false;
        }

        var after = text[(colon + 1)..].TrimStart();
        if (!after.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        speaker = candidate;
        rest = after;
        return true;
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }

    private static bool StartsWithAt(string text, int position, string value)
        => position >= 0 &&
           position + value.Length <= text.Length &&
           string.CompareOrdinal(text, position, value, 0, value.Length) == 0;

    private static int FindMatchingParen(string text, int openIndex)
    {
        var depth = 0;

        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static TextSpan SpanAfter(SourceLine line, string keyword, string token)
    {
        var keywordIndex = line.Text.IndexOf(keyword, StringComparison.Ordinal);
        var searchStart = keywordIndex < 0 ? 0 : keywordIndex + keyword.Length;
        var tokenIndex = searchStart <= line.Text.Length
            ? line.Text.IndexOf(token, searchStart, StringComparison.Ordinal)
            : -1;

        return tokenIndex < 0
            ? line.Span
            : new TextSpan(line.Span.Start + tokenIndex, token.Length);
    }

    private static string Unescape(string text)
        => text.Replace("\\\"", "\"").Replace("\\\\", "\\");
}
