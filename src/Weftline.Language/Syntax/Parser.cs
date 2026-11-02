using System.Text;
using Weftline.Language.Diagnostics;
using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// Weftline 的面向行递归下降解析器。
///
/// 解析器只负责语法与结构，不做符号解析；变量、角色、道具和正文引用的
/// 解析由 Binder 负责。`.weft` 与 `.wstory` 共用行级工具，但入口分开。
///
/// Line-oriented recursive-descent parser for Weftline.
///
/// The parser only handles syntax and structure. Symbol resolution belongs to
/// the Binder. `.weft` and `.wstory` share line helpers but have separate entry
/// points.
/// </summary>
public sealed partial class Parser
{
    private readonly SourceText _source;
    private readonly DiagnosticBag _diagnostics;
    private readonly HashSet<string> _moduleTitles = new(StringComparer.Ordinal);
    private int _index;

    private Parser(SourceText source, DiagnosticBag diagnostics)
    {
        _source = source;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// 根据文件名或内容自动选择 `.weft` 或 `.wstory` 解析。
    /// Automatically parses as `.weft` or `.wstory` based on file name or content.
    /// </summary>
    public static ScriptFileSyntax Parse(SourceText source, DiagnosticBag diagnostics)
    {
        var parser = new Parser(source, diagnostics);
        return parser.IsWStoryFile() ? parser.ParseWStoryFile() : parser.ParseWeftFile();
    }

    public static WeftFileSyntax ParseWeft(SourceText source, DiagnosticBag diagnostics)
        => new Parser(source, diagnostics).ParseWeftFile();

    public static WStoryFileSyntax ParseWStory(SourceText source, DiagnosticBag diagnostics)
        => new Parser(source, diagnostics).ParseWStoryFile();

    private bool IsWStoryFile()
    {
        if (_source.FilePath?.EndsWith(".wstory", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        foreach (var line in _source.Lines)
        {
            var trimmed = line.Text.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            return IsDirective(trimmed, "@project");
        }

        return false;
    }

    private SourceLine? Current => _index < _source.Lines.Count ? _source.Lines[_index] : null;

    private bool IsAtEnd => _index >= _source.Lines.Count;

    private string CurrentTrimmed => Current?.Text.Trim() ?? string.Empty;

    private WeftFileSyntax ParseWeftFile()
    {
        var variables = new List<WeftVariableSyntax>();
        var characters = new List<WeftCharacterSyntax>();
        var items = new List<WeftItemSyntax>();
        var scenes = new List<WeftSceneSyntax>();

        while (true)
        {
            SkipBlankAndComments();
            if (IsAtEnd)
            {
                break;
            }

            var line = Current!.Value;
            var trimmed = line.Text.Trim();

            if (IsDirective(trimmed, "@vars"))
            {
                Advance();
                ParseWeftVariables(variables);
                continue;
            }

            if (IsDirective(trimmed, "@chars"))
            {
                Advance();
                ParseWeftNames(trimmed, "@char", characters, static (name, span) => new WeftCharacterSyntax(name, span));
                continue;
            }

            if (IsDirective(trimmed, "@items"))
            {
                Advance();
                ParseWeftNames(trimmed, "@item", items, static (name, span) => new WeftItemSyntax(name, span));
                continue;
            }

            if (IsDirective(trimmed, "@scenes"))
            {
                Advance();
                ParseWeftNames(trimmed, "@scene", scenes, static (name, span) => new WeftSceneSyntax(name, span));
                continue;
            }

            if (IsDirective(trimmed, "@var"))
            {
                ParseWeftVariableLine(line, trimmed, variables);
                continue;
            }

            if (IsDirective(trimmed, "@char"))
            {
                ParseWeftNameLine(line, trimmed, "@char", characters, static (name, span) => new WeftCharacterSyntax(name, span));
                continue;
            }

            if (IsDirective(trimmed, "@item"))
            {
                ParseWeftNameLine(line, trimmed, "@item", items, static (name, span) => new WeftItemSyntax(name, span));
                continue;
            }

            if (IsDirective(trimmed, "@scene"))
            {
                ParseWeftNameLine(line, trimmed, "@scene", scenes, static (name, span) => new WeftSceneSyntax(name, span));
                continue;
            }

            break;
        }

        var modules = new List<ModuleSyntax>();

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
                continue;
            }

            if (IsDirective(trimmed, "@globals") ||
                IsDirective(trimmed, "@vars") ||
                IsDirective(trimmed, "@chars") ||
                IsDirective(trimmed, "@items") ||
                IsDirective(trimmed, "@scenes") ||
                IsDirective(trimmed, "@var") ||
                IsDirective(trimmed, "@char") ||
                IsDirective(trimmed, "@item") ||
                IsDirective(trimmed, "@scene"))
            {
                _diagnostics.Error(
                    DiagnosticCode.VarOutsideGlobals,
                    line.Span,
                    "符号声明必须放在模块之前的顶部分组里。");
                Advance();
                continue;
            }

            if (IsDirective(trimmed, "@project") ||
                IsDirective(trimmed, "@summary") ||
                IsDirective(trimmed, "@doc"))
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    line.Span,
                    "项目指令只能出现在 .wstory 文件中。");
                Advance();
                continue;
            }

            _diagnostics.Error(
                DiagnosticCode.UnknownLine,
                line.Span,
                "无法识别的行；模块必须以 @title 开始。");
            Advance();
        }

        return new WeftFileSyntax(
            variables,
            characters,
            items,
            scenes,
            modules,
            new TextSpan(0, _source.Length));
    }

    private void ParseWeftVariables(List<WeftVariableSyntax> variables)
    {
        while (true)
        {
            SkipBlankAndComments();
            if (IsAtEnd)
            {
                return;
            }

            var line = Current!.Value;
            var trimmed = line.Text.Trim();
            if (!IsDirective(trimmed, "@var"))
            {
                return;
            }

            ParseWeftVariableLine(line, trimmed, variables);
        }
    }

    private void ParseWeftVariableLine(
        SourceLine line,
        string trimmed,
        List<WeftVariableSyntax> variables)
    {
        Advance();

        if (!TryParseAssignment(trimmed, "@var", line, out var name, out var value))
        {
            return;
        }

        variables.Add(new WeftVariableSyntax(name, value, line.Span));
    }

    private void ParseWeftNames<T>(
        string groupLine,
        string keyword,
        List<T> target,
        Func<string, TextSpan, T> factory)
    {
        while (true)
        {
            SkipBlankAndComments();
            if (IsAtEnd)
            {
                return;
            }

            var line = Current!.Value;
            var trimmed = line.Text.Trim();
            if (!IsDirective(trimmed, keyword))
            {
                return;
            }

            ParseWeftNameLine(line, trimmed, keyword, target, factory);
        }
    }

    private void ParseWeftNameLine<T>(
        SourceLine line,
        string trimmed,
        string keyword,
        List<T> target,
        Func<string, TextSpan, T> factory)
    {
        Advance();

        if (!TryParseSingleName(trimmed, keyword, line, out var name))
        {
            return;
        }

        target.Add(factory(name, line.Span));
    }

    private ModuleSyntax ParseModule()
    {
        var titleLine = Current!.Value;
        Advance();

        var trimmedTitleLine = titleLine.Text.Trim();
        var title = trimmedTitleLine.Length > "@title".Length
            ? trimmedTitleLine["@title".Length..].Trim()
            : string.Empty;

        ValidateTitle(title, titleLine);

        var titleSpan = title.Length == 0
            ? titleLine.Span
            : SpanAfter(titleLine, "@title", title);

        string? scene = null;
        TextSpan? sceneSpan = null;
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

            if (IsDirective(trimmed, "@scene"))
            {
                if (seenContent)
                {
                    _diagnostics.Error(
                        DiagnosticCode.HeaderAfterContent,
                        line.Span,
                        "@scene 只能出现在模块内容之前。");
                }

                Advance();
                moduleEnd = line.Span.End;

                var name = trimmed["@scene".Length..].Trim();
                if (!SyntaxFacts.IsIdentifier(name))
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedScene,
                        line.Span,
                        "@scene 的语法是：@scene <场景名>");
                }
                else if (scene is not null)
                {
                    _diagnostics.Error(
                        DiagnosticCode.DuplicateScene,
                        line.Span,
                        $"模块内重复声明场景：{name}");
                }
                else
                {
                    scene = name;
                    sceneSpan = SpanAfter(line, "@scene", name);
                }

                continue;
            }

            if (IsDirective(trimmed, "@char") ||
                IsDirective(trimmed, "@item") ||
                IsDirective(trimmed, "@var") ||
                IsDirective(trimmed, "@globals") ||
                IsDirective(trimmed, "@vars") ||
                IsDirective(trimmed, "@chars") ||
                IsDirective(trimmed, "@items") ||
                IsDirective(trimmed, "@scenes"))
            {
                _diagnostics.Error(
                    DiagnosticCode.VarOutsideGlobals,
                    line.Span,
                    "符号声明必须放在模块之前的顶部分组里。");
                Advance();
                moduleEnd = line.Span.End;
                continue;
            }

            if (IsDirective(trimmed, "@set"))
            {
                _diagnostics.Error(
                    DiagnosticCode.SetInContent,
                    line.Span,
                    "正文不支持 @set；变量只能在出口分支效果中修改。");
                Advance();
                moduleEnd = line.Span.End;
                continue;
            }

            seenContent = true;
            ContentSyntax? parsed = null;

            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                parsed = ParseNarration();
            }
            else if (IsDirective(trimmed, "@fig"))
            {
                parsed = ParseFigure();
            }
            else if (TryGetDialoguePrefix(line.Text, out var speaker, out _))
            {
                parsed = ParseDialogue(line, speaker);
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
            scene,
            sceneSpan,
            content,
            exit,
            TextSpan.FromBounds(titleLine.Span.Start, moduleEnd));
    }

    private void ValidateTitle(string title, SourceLine titleLine)
    {
        if (title.Length == 0)
        {
            _diagnostics.Error(DiagnosticCode.EmptyTitle, titleLine.Span, "@title 不能为空。");
            return;
        }

        if (title.Contains('{') || title.Contains('}'))
        {
            _diagnostics.Error(
                DiagnosticCode.InvalidTitleCharacter,
                titleLine.Span,
                "@title 不能包含 { 或 }。");
        }

        if (string.Equals(title, "END", StringComparison.Ordinal))
        {
            _diagnostics.Error(
                DiagnosticCode.InvalidEnd,
                titleLine.Span,
                "END 是保留的终止模块，不能作为 @title 定义。");
        }

        if (!_moduleTitles.Add(title))
        {
            _diagnostics.Error(
                DiagnosticCode.DuplicateModuleTitle,
                titleLine.Span,
                $"模块标题重复：{title}");
        }
    }

    private NarrationSyntax ParseNarration()
    {
        var openingLine = Current!.Value;
        var open = openingLine.Text.IndexOf('[', StringComparison.Ordinal);
        var segments = ParseTextBlockSegments(openingLine, open, out var end);
        return new NarrationSyntax(segments, TextSpan.FromBounds(openingLine.Span.Start, end));
    }

    private DialogueSyntax ParseDialogue(SourceLine openingLine, string speaker)
    {
        var colon = openingLine.Text.IndexOf(':');
        var open = openingLine.Text.IndexOf('[', colon + 1);
        var segments = ParseTextBlockSegments(openingLine, open, out var end);
        return new DialogueSyntax(
            speaker,
            segments,
            TextSpan.FromBounds(openingLine.Span.Start, end));
    }

    private IReadOnlyList<TextSegmentSyntax> ParseTextBlockSegments(
        SourceLine openingLine,
        int openBracket,
        out int end)
    {
        var text = openingLine.Text;
        var segments = new List<TextSegmentSyntax>();
        var close = text.IndexOf(']', openBracket + 1);

        if (close >= 0)
        {
            var contentStart = openBracket + 1;
            segments.AddRange(ParseSegments(
                text[contentStart..close],
                openingLine.Span.Start + contentStart,
                trim: true));
            end = openingLine.Span.End;
            Advance();
            return segments;
        }

        var firstStart = openBracket + 1;
        var firstRaw = text[firstStart..];
        var firstLeading = firstRaw.Length - firstRaw.TrimStart().Length;
        segments.AddRange(ParseSegments(
            firstRaw.Trim(),
            openingLine.Span.Start + firstStart + firstLeading,
            trim: false));
        end = openingLine.Span.End;
        Advance();

        var closed = false;

        while (!IsAtEnd)
        {
            var line = Current!.Value;
            var lineText = line.Text;
            var closeIndex = lineText.IndexOf(']');

            if (closeIndex >= 0)
            {
                var raw = lineText[..closeIndex];
                var leading = raw.Length - raw.TrimStart().Length;
                segments.Add(new TextLiteralSyntax("\n", TextSpan.Empty(line.Span.Start)));
                segments.AddRange(ParseSegments(
                    raw.Trim(),
                    line.Span.Start + leading,
                    trim: false));
                end = line.Span.Start + closeIndex + 1;
                closed = true;
                Advance();
                break;
            }

            var lineRaw = lineText;
            var lineLeading = lineRaw.Length - lineRaw.TrimStart().Length;
            segments.Add(new TextLiteralSyntax("\n", TextSpan.Empty(line.Span.Start)));
            segments.AddRange(ParseSegments(lineRaw.Trim(), line.Span.Start + lineLeading, trim: false));
            end = line.Span.End;
            Advance();
        }

        if (!closed)
        {
            _diagnostics.Error(
                DiagnosticCode.UnterminatedTextBlock,
                openingLine.Span,
                "文本块缺少闭合的 ]。");
            end = LastLineEnd();
        }

        TrimSegments(segments);
        return segments;
    }

    private List<TextSegmentSyntax> ParseSegments(string text, int absoluteStart, bool trim)
    {
        var segments = new List<TextSegmentSyntax>();
        var literal = new StringBuilder();
        var literalStart = 0;
        var position = 0;

        void Flush(int endPosition)
        {
            if (literal.Length == 0)
            {
                return;
            }

            segments.Add(new TextLiteralSyntax(
                literal.ToString(),
                TextSpan.FromBounds(absoluteStart + literalStart, absoluteStart + endPosition)));
            literal.Clear();
            literalStart = endPosition;
        }

        while (position < text.Length)
        {
            var current = text[position];

            if (current == '{')
            {
                if (position + 1 < text.Length && text[position + 1] == '{')
                {
                    literal.Append('{');
                    position += 2;
                    continue;
                }

                Flush(position);

                var close = text.IndexOf('}', position + 1);
                var newline = text.IndexOf('\n', position + 1);
                if (close < 0 || (newline >= 0 && newline < close))
                {
                    _diagnostics.Error(
                        DiagnosticCode.UnterminatedReference,
                        new TextSpan(absoluteStart + position, 1),
                        "{ 引用没有在同一行闭合。");
                    literalStart = position;
                    literal.Append('{');
                    position++;
                    continue;
                }

                var raw = text[(position + 1)..close];
                var name = raw.Trim();
                var leading = raw.Length - raw.TrimStart().Length;

                if (!SyntaxFacts.IsIdentifier(name))
                {
                    _diagnostics.Error(
                        DiagnosticCode.MalformedDirective,
                        TextSpan.FromBounds(absoluteStart + position, absoluteStart + close + 1),
                        "引用必须是合法的符号名，例如 {萧影}。");
                    segments.Add(new TextLiteralSyntax(
                        text[position..(close + 1)],
                        TextSpan.FromBounds(absoluteStart + position, absoluteStart + close + 1)));
                }
                else
                {
                    segments.Add(new TextReferenceSyntax(
                        name,
                        new TextSpan(absoluteStart + position + 1 + leading, name.Length)));
                }

                position = close + 1;
                literalStart = position;
                continue;
            }

            if (current == '}')
            {
                if (position + 1 < text.Length && text[position + 1] == '}')
                {
                    literal.Append('}');
                    position += 2;
                    continue;
                }

                literal.Append('}');
                position++;
                continue;
            }

            literal.Append(current);
            position++;
        }

        Flush(text.Length);

        if (trim)
        {
            TrimSegments(segments);
        }

        return segments;
    }

    private static void TrimSegments(List<TextSegmentSyntax> segments)
    {
        if (segments.Count == 0)
        {
            return;
        }

        if (segments[0] is TextLiteralSyntax first)
        {
            var text = first.Text.TrimStart();
            if (text.Length == 0)
            {
                segments.RemoveAt(0);
            }
            else
            {
                var removed = first.Text.Length - text.Length;
                segments[0] = new TextLiteralSyntax(
                    text,
                    new TextSpan(first.Span.Start + removed, Math.Max(0, first.Span.Length - removed)));
            }
        }

        if (segments.Count == 0)
        {
            return;
        }

        var lastIndex = segments.Count - 1;
        if (segments[lastIndex] is TextLiteralSyntax last)
        {
            var text = last.Text.TrimEnd();
            if (text.Length == 0)
            {
                segments.RemoveAt(lastIndex);
            }
            else
            {
                var removed = last.Text.Length - text.Length;
                segments[lastIndex] = new TextLiteralSyntax(
                    text,
                    new TextSpan(last.Span.Start, Math.Max(0, last.Span.Length - removed)));
            }
        }
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

    private bool TryParseAssignment(
        string trimmed,
        string keyword,
        SourceLine line,
        out string name,
        out bool value)
    {
        name = string.Empty;
        value = false;

        var rest = trimmed[keyword.Length..].Trim();
        var parts = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2 ||
            !SyntaxFacts.IsIdentifier(parts[0]) ||
            !SyntaxFacts.TryParseBoolean(parts[1], out value))
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                line.Span,
                $"{keyword} 的语法是：{keyword} <布尔变量名> true|false");
            return false;
        }

        name = parts[0];
        return true;
    }

    private bool TryParseSingleName(
        string trimmed,
        string keyword,
        SourceLine line,
        out string name)
    {
        name = trimmed[keyword.Length..].Trim();

        if (!SyntaxFacts.IsIdentifier(name))
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                line.Span,
                $"{keyword} 的语法是：{keyword} <名称>");
            name = string.Empty;
            return false;
        }

        return true;
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
        return char.IsWhiteSpace(next) || next is '(' or ':' or '[';
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
