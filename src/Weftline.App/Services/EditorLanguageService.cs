using Weftline.Editor.Services;
using Weftline.Models;
using Weftline.Native;

namespace Weftline.App.Services;

/// <summary>
/// 编辑器语言服务：单文件语法诊断走 native parse，悬停/补全基于最近一次项目编译结果。
/// </summary>
public sealed class EditorLanguageService : IWeftEditorLanguageService
{
    private GraphProjection? _projection;
    private WeftProjectSymbols? _symbols;
    private Dictionary<string, GraphModuleNode> _modules = new(StringComparer.Ordinal);
    private Dictionary<string, GraphChapter> _chapters = new(StringComparer.Ordinal);
    private Dictionary<string, List<GraphModuleNode>> _modulesByScene = new(StringComparer.Ordinal);
    private Dictionary<string, WeftSymbolInfo> _variables = new(StringComparer.Ordinal);
    private Dictionary<string, WeftSymbolInfo> _characters = new(StringComparer.Ordinal);
    private Dictionary<string, WeftSymbolInfo> _items = new(StringComparer.Ordinal);
    private Dictionary<string, WeftSymbolInfo> _scenes = new(StringComparer.Ordinal);
    private Dictionary<string, WeftChapterInfo> _chapterInfos = new(StringComparer.Ordinal);

    public GraphProjection? Projection
    {
        get => _projection;
        set
        {
            _projection = value;
            _modules = value is null
                ? new Dictionary<string, GraphModuleNode>(StringComparer.Ordinal)
                : value.Modules.ToDictionary(static module => module.ModuleId, StringComparer.Ordinal);
            _chapters = value is null
                ? new Dictionary<string, GraphChapter>(StringComparer.Ordinal)
                : value.Chapters.ToDictionary(static chapter => chapter.Title, StringComparer.Ordinal);
            _modulesByScene = value is null
                ? new Dictionary<string, List<GraphModuleNode>>(StringComparer.Ordinal)
                : value.Modules
                    .SelectMany(static module => module.Scenes.Select(scene => (scene, module)))
                    .GroupBy(static pair => pair.scene, StringComparer.Ordinal)
                    .ToDictionary(
                        static group => group.Key,
                        static group => group.Select(static pair => pair.module).ToList(),
                        StringComparer.Ordinal);
        }
    }

    public WeftProjectSymbols? Symbols
    {
        get => _symbols;
        set
        {
            _symbols = value;
            _variables = ToMap(value?.Variables);
            _characters = ToMap(value?.Characters);
            _items = ToMap(value?.Items);
            _scenes = ToMap(value?.Scenes);
            _chapterInfos = value is null
                ? new Dictionary<string, WeftChapterInfo>(StringComparer.Ordinal)
                : value.Chapters.ToDictionary(static chapter => chapter.Title, StringComparer.Ordinal);
        }
    }

    public async ValueTask<IReadOnlyList<WeftDiagnostic>> GetSyntaxDiagnosticsAsync(
        string filePath,
        string text,
        long version,
        CancellationToken cancellationToken)
    {
        var response = await Task.Run(
            () => NativeWeftlineClient.Parse(filePath, text, version),
            cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return [];
        }

        var resolvedPath = string.IsNullOrEmpty(response.FilePath)
            ? filePath
            : response.FilePath;

        return response.Diagnostics
            .Select(diagnostic => new WeftDiagnostic(
                resolvedPath,
                new TextSpan(diagnostic.Start, diagnostic.Length),
                diagnostic.Severity switch
                {
                    "error" => WeftDiagnosticSeverity.Error,
                    "warning" => WeftDiagnosticSeverity.Warning,
                    _ => WeftDiagnosticSeverity.Info,
                },
                diagnostic.Code,
                diagnostic.Message))
            .ToList();
    }

    public ValueTask<WeftHoverInfo?> GetHoverAsync(
        WeftHoverContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(ResolveHover(context));

    public ValueTask<WeftCompletionResult> GetCompletionsAsync(
        WeftCompletionContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(ResolveCompletions(context));

    private WeftCompletionResult ResolveCompletions(WeftCompletionContext context)
    {
        var text = context.Text;
        var offset = Math.Clamp(context.Offset, 0, text.Length);
        var wordStart = Math.Clamp(context.WordStart, 0, offset);
        var prefix = text[wordStart..offset];

        var lineStart = offset == 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;
        var beforeCaret = text[lineStart..offset];
        var trimmedBefore = beforeCaret.TrimStart();

        var isDirectivePrefix = prefix.StartsWith('@');
        var afterSet = beforeCaret.LastIndexOf("@set ", StringComparison.Ordinal) >= 0;
        var afterScene = trimmedBefore.StartsWith("@scene ", StringComparison.Ordinal) ||
                         trimmedBefore.StartsWith("@scene\t", StringComparison.Ordinal);
        var afterArrow = beforeCaret.LastIndexOf("->", StringComparison.Ordinal) >= 0;
        var insideParen = IsInsideOpenParen(beforeCaret);
        var insideCondition = insideParen && IsConditionParen(beforeCaret);
        var insideCharacterSlot = insideParen && !insideCondition;
        var insideBraces = IsInsideBraces(beforeCaret);
        var isVarValueContext = IsVarValueContext(trimmedBefore, beforeCaret);
        var isDeclarationContext = IsDeclarationContext(trimmedBefore);
        var speakerCandidate = !isDirectivePrefix &&
                               !insideBraces &&
                               !afterArrow &&
                               !beforeCaret.Contains('[') &&
                               !beforeCaret.Contains(':') &&
                               trimmedBefore.Length > 0 &&
                               trimmedBefore == prefix;

        var items = new List<WeftCompletionItem>();

        if (isDirectivePrefix)
        {
            items.AddRange(GetDirectiveItems(context.FilePath));
        }
        else if (isVarValueContext)
        {
            items.Add(TrueItem);
            items.Add(FalseItem);
        }
        else if (afterSet || insideCondition)
        {
            items.AddRange(GetSymbolItems(_symbols?.Variables));
        }
        else if (afterScene)
        {
            items.AddRange(GetSymbolItems(_symbols?.Scenes));
        }
        else if (afterArrow)
        {
            items.AddRange(GetModuleItems());
            items.Add(EndItem);
        }
        else if (insideCharacterSlot)
        {
            items.AddRange(GetSymbolItems(_symbols?.Characters));
        }
        else if (insideBraces)
        {
            items.AddRange(GetSymbolItems(_symbols?.Characters));
            items.AddRange(GetSymbolItems(_symbols?.Items));
        }
        else if (isDeclarationContext)
        {
            // 声明点需要新名字，不提供已有符号，避免引导写出重复声明。
        }
        else if (speakerCandidate)
        {
            items.AddRange(GetSymbolItems(_symbols?.Characters));
        }
        else
        {
            items.AddRange(GetDirectiveItems(context.FilePath));
            items.AddRange(GetSymbolItems(_symbols?.Variables));
            items.AddRange(GetSymbolItems(_symbols?.Characters));
            items.AddRange(GetSymbolItems(_symbols?.Items));
            items.AddRange(GetSymbolItems(_symbols?.Scenes));
            items.AddRange(GetModuleItems());
            items.AddRange(GetChapterItems());
        }

        return new WeftCompletionResult(Filter(items, prefix), wordStart);
    }

    private WeftHoverInfo? ResolveHover(WeftHoverContext context)
    {
        if (string.IsNullOrEmpty(context.Text))
        {
            return null;
        }

        var (lineText, lineStart) = GetLine(context.Text, context.Offset);
        var trimmed = lineText.TrimStart();
        var leading = lineText.Length - trimmed.Length;
        var column = context.Offset - lineStart - leading;

        if (column < 0)
        {
            return null;
        }

        if (TryResolveDirective(trimmed, column) is { } directive)
        {
            return directive;
        }

        var setIndex = trimmed.IndexOf("@set ", StringComparison.Ordinal);
        if (setIndex >= 0 && column >= setIndex + "@set ".Length)
        {
            return ResolveVariable(FirstToken(trimmed[(setIndex + "@set ".Length)..]), "分支效果");
        }

        var arrow = trimmed.LastIndexOf("->", StringComparison.Ordinal);
        if (arrow >= 0 && column >= arrow + 2)
        {
            return ResolveModule(ExtractTarget(trimmed[(arrow + 2)..]));
        }

        if (TryResolveSpeaker(trimmed, column) is { } speaker)
        {
            return speaker;
        }

        if (TryResolveReference(lineText, column) is { } reference)
        {
            return ResolveReference(reference);
        }

        return null;
    }

    private WeftHoverInfo? TryResolveDirective(string trimmed, int column)
    {
        if (trimmed.StartsWith("@module ", StringComparison.Ordinal) &&
            column >= "@module ".Length)
        {
            return ResolveModule(trimmed["@module ".Length..].Trim());
        }

        if (trimmed.StartsWith("@title ", StringComparison.Ordinal) &&
            column >= "@title ".Length)
        {
            return ResolveChapter(trimmed["@title ".Length..].Trim());
        }

        if (trimmed.StartsWith("@scene ", StringComparison.Ordinal) &&
            column >= "@scene ".Length)
        {
            return ResolveScene(FirstToken(trimmed["@scene ".Length..]));
        }

        if (trimmed.StartsWith("@var ", StringComparison.Ordinal) &&
            column >= "@var ".Length)
        {
            return ResolveVariable(FirstToken(trimmed["@var ".Length..]), "变量");
        }

        if (trimmed.StartsWith("@char ", StringComparison.Ordinal) &&
            column >= "@char ".Length)
        {
            return ResolveCharacter(FirstToken(trimmed["@char ".Length..]));
        }

        if (trimmed.StartsWith("@item ", StringComparison.Ordinal) &&
            column >= "@item ".Length)
        {
            return ResolveItem(FirstToken(trimmed["@item ".Length..]));
        }

        return null;
    }

    private WeftHoverInfo? TryResolveSpeaker(string trimmed, int column)
    {
        if (trimmed.StartsWith('@'))
        {
            return null;
        }

        var colon = trimmed.IndexOf(':');
        if (colon <= 0 || column > colon)
        {
            return null;
        }

        return ResolveCharacter(trimmed[..colon].Trim());
    }

    private static string? TryResolveReference(string lineText, int column)
    {
        var open = lineText.LastIndexOf('{', Math.Min(column, Math.Max(0, lineText.Length - 1)));
        if (open < 0)
        {
            return null;
        }

        var close = lineText.IndexOf('}', open + 1);
        if (close < 0 || column > close)
        {
            return null;
        }

        var name = lineText[(open + 1)..close].Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private WeftHoverInfo? ResolveReference(string name)
    {
        if (_characters.TryGetValue(name, out var character))
        {
            return ToHover("角色", character);
        }

        if (_items.TryGetValue(name, out var item))
        {
            return ToHover("道具", item);
        }

        return null;
    }

    private WeftHoverInfo? ResolveVariable(string name, string kind)
        => !string.IsNullOrEmpty(name) && _variables.TryGetValue(name, out var symbol)
            ? ToHover(kind, symbol)
            : null;

    private WeftHoverInfo? ResolveCharacter(string name)
        => !string.IsNullOrEmpty(name) && _characters.TryGetValue(name, out var symbol)
            ? ToHover("角色", symbol)
            : null;

    private WeftHoverInfo? ResolveItem(string name)
        => !string.IsNullOrEmpty(name) && _items.TryGetValue(name, out var symbol)
            ? ToHover("道具", symbol)
            : null;

    private WeftHoverInfo? ResolveScene(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        _scenes.TryGetValue(name, out var symbol);
        var hasModules = _modulesByScene.TryGetValue(name, out var modules);

        if (symbol is null && !hasModules)
        {
            return null;
        }

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(symbol?.Description))
        {
            parts.Add(symbol.Description!);
        }

        if (hasModules)
        {
            parts.Add($"用于 {modules!.Count} 个模块");
        }

        var location = symbol is null || string.IsNullOrEmpty(symbol.Source.FilePath)
            ? null
            : Path.GetFileName(symbol.Source.FilePath);

        return new WeftHoverInfo($"场景 {name}", parts.Count > 0 ? string.Join(" · ", parts) : null, location);
    }

    private WeftHoverInfo? ResolveChapter(string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        if (_chapterInfos.TryGetValue(title, out var chapter))
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(chapter.Description))
            {
                parts.Add(chapter.Description!);
            }

            parts.Add($"模块数：{chapter.ModuleIds.Count}");

            var location = string.IsNullOrEmpty(chapter.Source.FilePath)
                ? null
                : Path.GetFileName(chapter.Source.FilePath);

            return new WeftHoverInfo($"章节 {chapter.Title}", string.Join(" · ", parts), location);
        }

        return _chapters.TryGetValue(title, out var projectionChapter)
            ? new WeftHoverInfo($"章节 {projectionChapter.Title}", $"模块数：{projectionChapter.ModuleIds.Count}")
            : null;
    }

    private WeftHoverInfo? ResolveModule(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId) ||
            string.Equals(moduleId, "END", StringComparison.Ordinal) ||
            !_modules.TryGetValue(moduleId, out var module))
        {
            return null;
        }

        var chapter = _chapters.TryGetValue(module.ChapterId, out var found)
            ? found.Title
            : module.ChapterId;

        var parts = new List<string> { $"章节：{chapter}" };

        if (module.Scenes.Count > 0)
        {
            parts.Add($"场景：{string.Join(" / ", module.Scenes)}");
        }

        parts.Add(
            $"分支：{module.BranchCount} · 对话：{module.Summary.DialogueCount} · 选项：{module.Summary.ChoiceCount}");

        if (!string.IsNullOrWhiteSpace(module.Summary.FirstLine))
        {
            parts.Add($"首行：{module.Summary.FirstLine}");
        }

        var location = string.IsNullOrEmpty(module.Source.FilePath)
            ? null
            : Path.GetFileName(module.Source.FilePath);

        return new WeftHoverInfo($"模块 {module.ModuleId}", string.Join(" · ", parts), location);
    }

    private static readonly WeftCompletionItem[] WeftDirectives =
    [
        new("@title", "@title", "章节标题"),
        new("@module", "@module", "模块"),
        new("@next", "@next", "出口"),
        new("@scene", "@scene", "场景标签"),
        new("@vars", "@vars", "变量分组"),
        new("@var", "@var", "变量声明"),
        new("@chars", "@chars", "角色分组"),
        new("@char", "@char", "角色声明"),
        new("@items", "@items", "道具分组"),
        new("@item", "@item", "道具声明"),
        new("@scenes", "@scenes", "场景分组"),
        new("@fig", "@fig", "插入图片"),
        new("@set", "@set", "分支效果"),
    ];

    private static readonly WeftCompletionItem[] WStoryDirectives =
    [
        new("@project", "@project", "项目信息"),
        new("@summary", "@summary", "项目概要"),
        new("@chapters", "@chapters", "章节分组"),
        new("@chapter", "@chapter", "章节"),
        new("@docs", "@docs", "设定文档分组"),
        new("@doc", "@doc", "设定文档"),
        new("@chars", "@chars", "角色分组"),
        new("@char", "@char", "角色声明"),
        new("@items", "@items", "道具分组"),
        new("@item", "@item", "道具声明"),
        new("@scenes", "@scenes", "场景分组"),
        new("@scene", "@scene", "场景声明"),
        new("@vars", "@vars", "变量分组"),
        new("@var", "@var", "变量声明"),
    ];

    private static readonly WeftCompletionItem EndItem = new("END", "END", "结束");
    private static readonly WeftCompletionItem TrueItem = new("true", "true", "布尔值 true");
    private static readonly WeftCompletionItem FalseItem = new("false", "false", "布尔值 false");

    private IEnumerable<WeftCompletionItem> GetDirectiveItems(string filePath)
        => Path.GetExtension(filePath).Equals(".wstory", StringComparison.OrdinalIgnoreCase)
            ? WStoryDirectives
            : WeftDirectives;

    private IEnumerable<WeftCompletionItem> GetSymbolItems(
        IReadOnlyList<WeftSymbolInfo>? symbols)
        => symbols?.Select(ToCompletionItem) ?? [];

    private IEnumerable<WeftCompletionItem> GetChapterItems()
        => _symbols?.Chapters.Select(static chapter =>
            new WeftCompletionItem(chapter.Title, chapter.Title, chapter.Description)) ?? [];

    private IEnumerable<WeftCompletionItem> GetModuleItems()
    {
        if (_projection is null)
        {
            return [];
        }

        return _projection.Modules.Select(module =>
        {
            var chapter = _chapters.TryGetValue(module.ChapterId, out var found)
                ? found.Title
                : module.ChapterId;
            var parts = new List<string> { $"章节：{chapter}" };

            if (module.Scenes.Count > 0)
            {
                parts.Add($"场景：{string.Join(" / ", module.Scenes)}");
            }

            parts.Add($"分支：{module.BranchCount}");
            return new WeftCompletionItem(
                module.ModuleId,
                module.ModuleId,
                string.Join(" · ", parts));
        });
    }

    private static WeftCompletionItem ToCompletionItem(WeftSymbolInfo symbol)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(symbol.Description))
        {
            parts.Add(symbol.Description!);
        }

        if (symbol.Initial is { } initial)
        {
            parts.Add($"初始值：{initial}");
        }

        return new WeftCompletionItem(
            symbol.Name,
            symbol.Name,
            parts.Count > 0 ? string.Join(" · ", parts) : null);
    }

    private static bool IsInsideBraces(string beforeCaret)
    {
        var open = beforeCaret.LastIndexOf('{');
        if (open < 0)
        {
            return false;
        }

        return open > beforeCaret.LastIndexOf('}');
    }

    private static bool IsVarValueContext(string trimmedBefore, string beforeCaret)
    {
        if (!trimmedBefore.StartsWith("@var ", StringComparison.Ordinal) &&
            !trimmedBefore.StartsWith("@var\t", StringComparison.Ordinal))
        {
            return false;
        }

        if (beforeCaret.Contains('['))
        {
            return false;
        }

        if (beforeCaret.EndsWith(' '))
        {
            return true;
        }

        var tokens = trimmedBefore.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length >= 3;
    }

    private static bool IsDeclarationContext(string trimmedBefore)
        => trimmedBefore.StartsWith("@var ", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@var\t", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@char ", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@char\t", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@item ", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@item\t", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@module ", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@module\t", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@title ", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@title\t", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@chapter ", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@chapter\t", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@doc ", StringComparison.Ordinal) ||
           trimmedBefore.StartsWith("@doc\t", StringComparison.Ordinal);

    private static bool IsInsideOpenParen(string beforeCaret)
    {
        var open = beforeCaret.LastIndexOf('(');
        return open >= 0 && beforeCaret.LastIndexOf(')') < open;
    }

    private static bool IsConditionParen(string beforeCaret)
    {
        var index = beforeCaret.LastIndexOf('(') - 1;

        while (index >= 0 && char.IsWhiteSpace(beforeCaret[index]))
        {
            index--;
        }

        return index >= 0 && beforeCaret[index] == ':';
    }

    private static IReadOnlyList<WeftCompletionItem> Filter(
        List<WeftCompletionItem> items,
        string prefix)
        => items
            .Where(item => prefix.Length == 0 ||
                item.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(static item => item.Text, StringComparer.Ordinal)
            .ToList();

    private static WeftHoverInfo ToHover(string kind, WeftSymbolInfo symbol)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(symbol.Description))
        {
            parts.Add(symbol.Description!);
        }

        if (symbol.Initial is { } initial)
        {
            parts.Add($"初始值：{initial}");
        }

        var location = string.IsNullOrEmpty(symbol.Source.FilePath)
            ? null
            : Path.GetFileName(symbol.Source.FilePath);

        return new WeftHoverInfo(
            $"{kind} {symbol.Name}",
            parts.Count > 0 ? string.Join(" · ", parts) : null,
            location);
    }

    private static Dictionary<string, WeftSymbolInfo> ToMap(IReadOnlyList<WeftSymbolInfo>? symbols)
        => symbols is null
            ? new Dictionary<string, WeftSymbolInfo>(StringComparer.Ordinal)
            : symbols.ToDictionary(static symbol => symbol.Name, StringComparer.Ordinal);

    private static string FirstToken(string text)
    {
        var value = text.Trim();
        var separator = value.IndexOfAny([' ', '\t']);
        return (separator >= 0 ? value[..separator] : value).Trim();
    }

    private static string ExtractTarget(string text)
    {
        var value = text.Trim();

        var brace = value.IndexOf('{');
        if (brace >= 0)
        {
            value = value[..brace];
        }

        var separator = value.IndexOfAny([' ', '\t']);
        return (separator >= 0 ? value[..separator] : value).Trim();
    }

    private static (string LineText, int LineStart) GetLine(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var start = offset == 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;
        var end = text.IndexOf('\n', offset);

        if (end < 0)
        {
            end = text.Length;
        }

        return (text[start..end].TrimEnd('\r'), start);
    }
}
