using Weftline.Language.Diagnostics;
using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

public sealed partial class Parser
{
    private WStoryFileSyntax ParseWStoryFile()
    {
        SkipBlankAndComments();

        if (IsAtEnd || !IsDirective(CurrentTrimmed, "@project"))
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                Current?.Span ?? new TextSpan(0, 0),
                ".wstory 必须以 @project 开始。");

            return new WStoryFileSyntax(
                string.Empty,
                new TextSpan(0, 0),
                null,
                Array.Empty<DocSyntax>(),
                Array.Empty<WStoryCharacterSyntax>(),
                Array.Empty<WStoryItemSyntax>(),
                Array.Empty<WStorySceneSyntax>(),
                Array.Empty<WStoryVariableSyntax>(),
                new TextSpan(0, _source.Length));
        }

        var projectLine = Current!.Value;
        Advance();

        var trimmed = projectLine.Text.Trim();
        var name = trimmed.Length > "@project".Length
            ? trimmed["@project".Length..].Trim()
            : string.Empty;

        if (name.Length == 0)
        {
            _diagnostics.Error(DiagnosticCode.EmptyTitle, projectLine.Span, "@project 不能为空。");
        }

        var nameSpan = name.Length == 0
            ? projectLine.Span
            : SpanAfter(projectLine, "@project", name);

        TextBlockSyntax? summary = null;
        var docs = new List<DocSyntax>();
        var characters = new List<WStoryCharacterSyntax>();
        var items = new List<WStoryItemSyntax>();
        var scenes = new List<WStorySceneSyntax>();
        var variables = new List<WStoryVariableSyntax>();

        while (true)
        {
            SkipBlankAndComments();
            if (IsAtEnd)
            {
                break;
            }

            var line = Current!.Value;
            var current = line.Text.Trim();

            if (IsDirective(current, "@summary"))
            {
                Advance();
                summary = ParseSummary(line);
                continue;
            }

            if (IsDirective(current, "@docs"))
            {
                Advance();
                ParseDocs(docs);
                continue;
            }

            if (IsDirective(current, "@chars"))
            {
                Advance();
                ParseWStoryNames(current, "@char", characters, static (entry, description, span) => new WStoryCharacterSyntax(entry, description, span));
                continue;
            }

            if (IsDirective(current, "@items"))
            {
                Advance();
                ParseWStoryNames(current, "@item", items, static (entry, description, span) => new WStoryItemSyntax(entry, description, span));
                continue;
            }

            if (IsDirective(current, "@scenes"))
            {
                Advance();
                ParseWStoryNames(current, "@scene", scenes, static (entry, description, span) => new WStorySceneSyntax(entry, description, span));
                continue;
            }

            if (IsDirective(current, "@vars"))
            {
                Advance();
                ParseWStoryVariables(variables);
                continue;
            }

            if (IsDirective(current, "@set"))
            {
                _diagnostics.Error(
                    DiagnosticCode.SetInProjectFile,
                    line.Span,
                    ".wstory 不允许 @set；变量初始值由 .weft 的 @var 决定。");
                Advance();
                continue;
            }

            if (IsDirective(current, "@title") ||
                IsDirective(current, "@next") ||
                current.StartsWith("[", StringComparison.Ordinal))
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    line.Span,
                    ".wstory 只能包含 @project、@summary、@docs、@chars、@items、@scenes 和 @vars。");
                Advance();
                continue;
            }

            _diagnostics.Error(
                DiagnosticCode.UnknownLine,
                line.Span,
                "无法识别的 .wstory 行。");
            Advance();
        }

        return new WStoryFileSyntax(
            name,
            nameSpan,
            summary,
            docs,
            characters,
            items,
            scenes,
            variables,
            new TextSpan(0, _source.Length));
    }

    private TextBlockSyntax ParseSummary(SourceLine line)
    {
        var bracket = line.Text.IndexOf('[', StringComparison.Ordinal);
        if (bracket < 0)
        {
            _diagnostics.Error(
                DiagnosticCode.MalformedDirective,
                line.Span,
                "@summary 需要写成 @summary [文本]。");
            Advance();
            return new TextBlockSyntax(string.Empty, line.Span);
        }

        return ParsePlainTextBlockFrom(line, bracket);
    }

    private void ParseDocs(List<DocSyntax> docs)
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
            if (!IsDirective(trimmed, "@doc"))
            {
                return;
            }

            var bracket = line.Text.IndexOf('[', StringComparison.Ordinal);
            var declarationEnd = bracket >= 0
                ? trimmed.IndexOf('[', StringComparison.Ordinal)
                : trimmed.Length;

            var title = declarationEnd > "@doc".Length
                ? trimmed["@doc".Length..declarationEnd].Trim()
                : string.Empty;

            var titleSpan = title.Length == 0
                ? line.Span
                : SpanAfter(line, "@doc", title);

            TextBlockSyntax body;
            int end;

            if (bracket >= 0)
            {
                body = ParsePlainTextBlockFrom(line, bracket);
                end = body.Span.End;
            }
            else
            {
                Advance();
                body = new TextBlockSyntax(string.Empty, line.Span);
                end = line.Span.End;
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    line.Span,
                    "@doc 需要写成 @doc <标题> [文本]。");
            }

            if (title.Length == 0)
            {
                _diagnostics.Error(DiagnosticCode.EmptyTitle, line.Span, "@doc 需要一个标题。");
            }

            docs.Add(new DocSyntax(title, titleSpan, body, TextSpan.FromBounds(line.Span.Start, end)));
        }
    }

    private void ParseWStoryNames<T>(
        string groupLine,
        string keyword,
        List<T> target,
        Func<string, TextBlockSyntax?, TextSpan, T> factory)
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

            var bracket = line.Text.IndexOf('[', StringComparison.Ordinal);
            var declarationEnd = bracket >= 0
                ? trimmed.IndexOf('[', StringComparison.Ordinal)
                : trimmed.Length;

            var entry = declarationEnd > keyword.Length
                ? trimmed[keyword.Length..declarationEnd].Trim()
                : string.Empty;

            TextBlockSyntax? description = null;
            int end;

            if (bracket >= 0)
            {
                description = ParsePlainTextBlockFrom(line, bracket);
                end = description.Span.End;
            }
            else
            {
                Advance();
                end = line.Span.End;
            }

            if (!SyntaxFacts.IsIdentifier(entry))
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    line.Span,
                    $"{keyword} 的语法是：{keyword} <名称> [描述]。");
                continue;
            }

            target.Add(factory(entry, description, TextSpan.FromBounds(line.Span.Start, end)));
        }
    }

    private void ParseWStoryVariables(List<WStoryVariableSyntax> variables)
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

            var bracket = line.Text.IndexOf('[', StringComparison.Ordinal);
            var declarationEnd = bracket >= 0
                ? trimmed.IndexOf('[', StringComparison.Ordinal)
                : trimmed.Length;

            var declaration = declarationEnd > "@var".Length
                ? trimmed["@var".Length..declarationEnd].Trim()
                : string.Empty;

            TextBlockSyntax? description = null;
            int end;

            if (bracket >= 0)
            {
                description = ParsePlainTextBlockFrom(line, bracket);
                end = description.Span.End;
            }
            else
            {
                Advance();
                end = line.Span.End;
            }

            var parts = declaration.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !SyntaxFacts.IsIdentifier(parts[0]) ||
                !SyntaxFacts.TryParseBoolean(parts[1], out var value))
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    line.Span,
                    "@var 的语法是：@var <布尔变量名> true|false [描述]。");
                continue;
            }

            variables.Add(new WStoryVariableSyntax(
                parts[0],
                value,
                description,
                TextSpan.FromBounds(line.Span.Start, end)));
        }
    }

    private TextBlockSyntax ParsePlainTextBlockFrom(SourceLine openingLine, int openBracket)
    {
        var text = openingLine.Text;
        var close = text.IndexOf(']', openBracket + 1);

        if (close >= 0)
        {
            var inline = text[(openBracket + 1)..close].Trim();
            Advance();
            return new TextBlockSyntax(inline, openingLine.Span);
        }

        var lines = new List<string>
        {
            text[(openBracket + 1)..].Trim()
        };

        var end = openingLine.Span.End;
        Advance();

        var closed = false;

        while (!IsAtEnd)
        {
            var line = Current!.Value;
            var closeIndex = line.Text.IndexOf(']');

            if (closeIndex >= 0)
            {
                lines.Add(line.Text[..closeIndex].Trim());
                end = line.Span.Start + closeIndex + 1;
                closed = true;
                Advance();
                break;
            }

            lines.Add(line.Text.Trim());
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

        while (lines.Count > 0 && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return new TextBlockSyntax(
            string.Join("\n", lines),
            TextSpan.FromBounds(openingLine.Span.Start, end));
    }
}
