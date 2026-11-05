using Weftline.Language.Diagnostics;
using Weftline.Language.Source;

namespace Weftline.Language.Syntax;

// `.wstory` 项目总览的解析入口。本文件与 Parser.cs 共享行级工具，
// 只负责项目级指令；缩进仅用于显示，解析时全部通过 Trim 忽略。
// Entry points for `.wstory` project overviews. This file shares the line
// helpers in Parser.cs and only handles project-level directives. Indentation is
// display-only and ignored by trimming.
public sealed partial class Parser
{
    /// <summary>
    /// 解析一个 `.wstory` 项目总览文件。
    ///
    /// 文件必须以 @project 开始，后面是 @summary、@chapters、@docs、
    /// @chars、@items、@scenes、@vars 等顶层分组。这里的声明是工具生成的
    /// 描述性副本，真正的符号声明在 `.weft` 中。
    ///
    /// Parses one `.wstory` project overview file.
    ///
    /// The file must start with @project, followed by top-level groups such as
    /// @summary, @chapters, @docs, @chars, @items, @scenes, and @vars. These
    /// declarations are generated descriptive copies; the real symbol
    /// declarations live in `.weft` files.
    /// </summary>
    private WStoryFileSyntax ParseWStoryFile()
    {
        SkipBlankAndComments();

        // @project 必须是第一条有效行；否则报错并返回空语法树。
        // @project must be the first meaningful line; otherwise report and return an empty tree.
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
                Array.Empty<WStoryChapterSyntax>(),
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
        var chapters = new List<WStoryChapterSyntax>();
        var docs = new List<DocSyntax>();
        var characters = new List<WStoryCharacterSyntax>();
        var items = new List<WStoryItemSyntax>();
        var scenes = new List<WStorySceneSyntax>();
        var variables = new List<WStoryVariableSyntax>();

        // 顶层分组可以任意顺序出现；每个分组内部由对应的 ParseXxx 循环消费，
        // 因此这里只需要识别分组头并把控制权交出去。
        // Top-level groups may appear in any order. Each ParseXxx loop consumes
        // its own entries, so this loop only recognizes group headers and delegates.
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

            if (IsDirective(current, "@chapters"))
            {
                Advance();
                ParseChapters(chapters);
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
                IsDirective(current, "@chapter") ||
                current.StartsWith("[", StringComparison.Ordinal))
            {
                _diagnostics.Error(
                    DiagnosticCode.MalformedDirective,
                    line.Span,
                    ".wstory 只能包含 @project、@summary、@chapters、@docs、@chars、@items、@scenes 和 @vars。");
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
            chapters,
            docs,
            characters,
            items,
            scenes,
            variables,
            new TextSpan(0, _source.Length));
    }

    /// <summary>
    /// 解析单行概要：@summary [文本]。
    /// 方括号可以跨行，实际读取由 ParsePlainTextBlockFrom 完成。
    ///
    /// Parses the single-line summary @summary [text]. The bracket may span
    /// multiple lines; ParsePlainTextBlockFrom does the actual reading.
    /// </summary>
    private TextBlockSyntax ParseSummary(SourceLine line)
    {
        // 概要必须带方括号；缺失时消费当前行并返回空文本，保证解析继续。
        // The summary must have brackets; on failure consume the line and return empty text.
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

    /// <summary>
    /// 解析 @chapters 分组下的 @chapter 条目：@chapter &lt;标题&gt; [描述]。
    /// 描述可省略；标题与描述在第一个 [ 处分开。
    ///
    /// Parses @chapter entries under @chapters: @chapter &lt;title&gt; [description].
    /// The description is optional and is split from the title at the first [.
    /// </summary>
    private void ParseChapters(List<WStoryChapterSyntax> chapters)
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
            if (!IsDirective(trimmed, "@chapter"))
            {
                return;
            }

            // bracket 是原始行中的位置，用于交给 ParsePlainTextBlockFrom；
            // declarationEnd 是 trimmed 中的位置，只用于切出标题。
            // bracket is the position in the raw line and is passed to
            // ParsePlainTextBlockFrom; declarationEnd is the position in trimmed
            // and is only used to slice out the title.
            var bracket = line.Text.IndexOf('[', StringComparison.Ordinal);
            var declarationEnd = bracket >= 0
                ? trimmed.IndexOf('[', StringComparison.Ordinal)
                : trimmed.Length;

            var title = declarationEnd > "@chapter".Length
                ? trimmed["@chapter".Length..declarationEnd].Trim()
                : string.Empty;

            var titleSpan = title.Length == 0
                ? line.Span
                : SpanAfter(line, "@chapter", title);

            // 有 [ 时解析描述（可能跨行）；没有 [ 时只消费当前行。
            // With [, parse the description (possibly multi-line); otherwise consume only this line.
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

            if (title.Length == 0)
            {
                _diagnostics.Error(DiagnosticCode.EmptyTitle, line.Span, "@chapter 需要一个标题。");
            }

            chapters.Add(new WStoryChapterSyntax(
                title,
                titleSpan,
                description,
                TextSpan.FromBounds(line.Span.Start, end)));
        }
    }

    /// <summary>
    /// 解析 @docs 分组下的设定文档：@doc &lt;标题&gt; [正文]。
    /// 与 @chapter 不同，@doc 必须带正文方括号。
    ///
    /// Parses world-setting docs under @docs: @doc &lt;title&gt; [body].
    /// Unlike @chapter, @doc requires the body brackets.
    /// </summary>
    /// <remarks>
    /// @doc 是作者的设定资料，不属于剧情执行模型；解析器只保留标题和正文。
    /// 完整剧本写在 `.weft`，实体短描述写在对应的声明分组。
    ///
    /// @doc holds authoring notes, not part of the executable story; the parser
    /// keeps only the title and body. Script text belongs in `.weft`, and short
    /// entity descriptions belong in their declaration groups.
    /// </remarks>
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

            // @doc 的正文是必需的；缺失时仍然消费当前行，避免卡在同一行。
            // The @doc body is required; when missing, still consume the line to avoid getting stuck.
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

    /// <summary>
    /// 解析一组「名字 + 可选描述」条目，供 @chars / @items / @scenes 共用。
    /// 语法为 &lt;keyword&gt; &lt;名称&gt; [描述]，名称必须是合法标识符。
    /// 具体节点类型由 factory 决定。
    ///
    /// Parses a group of "name + optional description" entries shared by
    /// @chars, @items, and @scenes. The syntax is &lt;keyword&gt; &lt;name&gt; [description];
    /// the name must be a valid identifier. The factory decides the concrete node type.
    /// </summary>
    /// <param name="groupLine">
    /// 分组头行，目前未参与解析，保留以对齐调用处。
    /// The group header line; currently unused by parsing, kept for call-site symmetry.
    /// </param>
    /// <param name="keyword">条目关键字，例如 @char / @item / @scene。 / The entry keyword, e.g. @char / @item / @scene.</param>
    /// <param name="target">收集解析结果的列表。 / The list receiving parsed entries.</param>
    /// <param name="factory">把名字、描述和范围转换成具体语法节点。 / Converts a name, description, and span into a concrete syntax node.</param>
    /// <typeparam name="T">条目语法节点类型。 / The entry syntax node type.</typeparam>
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

            // 与 @chapter 相同：原始行位置用于读描述，trimmed 位置用于切名字。
            // Same as @chapter: the raw-line position reads the description, the
            // trimmed position slices the name.
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

    /// <summary>
    /// 解析 @vars 分组下的 @var 条目：@var &lt;布尔变量名&gt; true|false [描述]。
    /// 变量初值必须显式写出；描述可选。
    ///
    /// Parses @var entries under @vars: @var &lt;bool name&gt; true|false [description].
    /// The initial value is mandatory; the description is optional.
    /// </summary>
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

            // 声明部分必须恰好是「变量名 + 布尔初值」两段。
            // The declaration part must be exactly two tokens: name and boolean value.
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

    /// <summary>
    /// 从指定方括号开始读取一个纯文本块，供 @summary、@chapter、@doc、
    /// 声明描述等复用。
    ///
    /// 与 `.weft` 的正文块不同，这里不解析 {名字} 引用，只保留纯文本；
    /// 每行去掉首尾空白，多行之间用 \n 连接。无论是否闭合，都会消费到
    /// 闭合的 ] 所在行（或文件末尾）。
    ///
    /// Reads a plain text block starting at the given bracket. It is shared by
    /// @summary, @chapter, @doc, and declaration descriptions.
    ///
    /// Unlike `.weft` content blocks, this does not parse {name} references and
    /// keeps plain text only. Each line is trimmed and multiple lines are joined
    /// with \n. Whether or not the block closes, parsing consumes through the
    /// line containing the closing ] (or EOF).
    /// </summary>
    private TextBlockSyntax ParsePlainTextBlockFrom(SourceLine openingLine, int openBracket)
    {
        var text = openingLine.Text;
        var close = text.IndexOf(']', openBracket + 1);

        // 单行快路径：] 在同一行闭合，直接取中间文本。
        // Fast path: ] closes on the same line, so take the text in between.
        if (close >= 0)
        {
            var inline = text[(openBracket + 1)..close].Trim();
            Advance();
            return new TextBlockSyntax(inline, openingLine.Span);
        }

        // 多行：先收下开括号之后的剩余内容，再逐行读到 ]。
        // Multi-line: collect the remainder after the opening bracket, then read
        // line by line until ].
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
                // end 指向 ] 之后；同一行 ] 之后的内容随行一起被消费并忽略。
                // end points just past ]; anything after ] on this line is consumed and ignored.
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

        // 去掉块首尾的空行，避免多行描述产生多余的换行。
        // Drop leading and trailing empty lines so multi-line descriptions do not add stray newlines.
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
