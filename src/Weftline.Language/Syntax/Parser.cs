using Weftline.Language.Diagnostics;
using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

/// <summary>
/// Step 1 的行式递归下降解析器。
///
/// 它不做完整的词法 Token 流，而是把每一行当作一个“行 token”：
/// 空行、注释、指令、正文围栏、普通行。Parser 按当前模块的状态消费这些行。
///
/// 已实现：globals、模块头、旁白、对话、图片、@set、exit 原文捕获。
/// 留到 Step 2：exit 分支、条件表达式、分支效果、目标解析、StoryGraph IR。
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

            if (parts.Length != 2 || !IsIdentifier(parts[0]) || !TryParseBoolean(parts[1], out var value))
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
                exit = ParseRawExit();
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
                if (!IsIdentifier(name))
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
    /// 消费一个多行文本块。调用时 Current 指向开头的 [ 或 角色:[ 行。
    /// 块内所有内容按纯文本处理，只有单独一行 ] 才会结束。
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

        if (parts.Length != 2 || !IsIdentifier(parts[0]) || !TryParseBoolean(parts[1], out var value))
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

    private ExitSyntax ParseRawExit()
    {
        var startLine = Current!.Value;
        var lines = new List<SourceLine>();

        while (!IsAtEnd)
        {
            var line = Current!.Value;
            if (IsDirective(line.Text.Trim(), "@title"))
            {
                break;
            }

            lines.Add(line);
            Advance();
        }

        // 文件末尾或下一个模块前的空行不属于出口块，去掉尾部空行。
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1].Text))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var end = lines.Count == 0 ? startLine.Span.End : lines[^1].Span.End;

        return new ExitSyntax(
            TextSpan.FromBounds(startLine.Span.Start, end),
            lines.Select(static line => line.Text).ToArray());
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
        if (!IsIdentifier(candidate))
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

    private static bool IsIdentifier(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!char.IsLetter(text[0]) && text[0] != '_')
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseBoolean(string text, out bool value)
    {
        switch (text)
        {
            case "true":
                value = true;
                return true;
            case "false":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
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
