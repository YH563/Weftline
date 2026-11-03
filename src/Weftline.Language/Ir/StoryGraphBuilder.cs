using Weftline.Language.Binding;
using Weftline.Language.Projects;
using Weftline.Language.Syntax;

namespace Weftline.Language.Ir;

/// <summary>
/// 从项目 AST 与项目索引构建 StoryGraph。
/// Builds a StoryGraph from project ASTs and the project index.
/// </summary>
public static class StoryGraphBuilder
{
    public static StoryGraph Build(
        LoadedProject project,
        ProjectIndex index,
        string? entry = null)
    {
        var moduleChapters = index.Modules.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.Chapter,
            StringComparer.Ordinal);

        var chapterDescriptions = project.WStory?.Chapters.ToDictionary(
            static chapter => chapter.Title,
            static chapter => chapter.Description?.Text,
            StringComparer.Ordinal) ?? new Dictionary<string, string?>(StringComparer.Ordinal);

        var chapterModules = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var chapterOrder = new List<string>();

        foreach (var file in project.WeftFiles)
        {
            if (!chapterModules.TryGetValue(file.Ast.Title, out var modulesInChapter))
            {
                modulesInChapter = new List<string>();
                chapterModules[file.Ast.Title] = modulesInChapter;
                chapterOrder.Add(file.Ast.Title);
            }

            foreach (var module in file.Ast.Modules)
            {
                modulesInChapter.Add(module.Title);
            }
        }

        var chapters = chapterOrder
            .Select(title => new IrChapter(
                title,
                chapterDescriptions.TryGetValue(title, out var description) ? description : null,
                chapterModules[title]))
            .ToList();

        var modules = new List<IrModule>();
        var chapterEdges = new Dictionary<(string From, string To), List<string>>();

        foreach (var file in project.WeftFiles)
        {
            foreach (var module in file.Ast.Modules)
            {
                modules.Add(BuildModule(module, file.Ast.Title, moduleChapters));

                if (module.Exit is not { } exit)
                {
                    continue;
                }

                foreach (var branch in exit.Branches)
                {
                    if (!IsExternal(branch.Target, file.Ast.Title, moduleChapters) ||
                        !moduleChapters.TryGetValue(branch.Target, out var targetChapter))
                    {
                        continue;
                    }

                    if (!chapterEdges.TryGetValue((file.Ast.Title, targetChapter), out var via))
                    {
                        via = new List<string>();
                        chapterEdges[(file.Ast.Title, targetChapter)] = via;
                    }

                    var edge = $"{module.Title} -> {branch.Target}";
                    if (!via.Contains(edge, StringComparer.Ordinal))
                    {
                        via.Add(edge);
                    }
                }
            }
        }

        var projectInfo = project.WStory is { } wstory
            ? new IrProjectInfo(
                wstory.ProjectName,
                wstory.Summary?.Text,
                wstory.Docs.Select(static doc => new IrDoc(doc.Title, doc.Body.Text)).ToList())
            : new IrProjectInfo(
                Path.GetFileName(Path.TrimEndingDirectorySeparator(project.Folder)),
                null,
                Array.Empty<IrDoc>());

        var globals = index.Symbols.Values
            .Where(static symbol => symbol.Kind == SymbolKind.Variable)
            .ToDictionary(
                static symbol => symbol.Name,
                static symbol => symbol.Value ?? false,
                StringComparer.Ordinal);

        var entryId = entry ?? modules.FirstOrDefault()?.Id;

        return new StoryGraph(
            1,
            "END",
            entryId,
            projectInfo,
            chapters,
            globals,
            modules,
            chapterEdges
                .Select(static pair => new IrChapterEdge(pair.Key.From, pair.Key.To, pair.Value))
                .ToList());
    }

    private static IrModule BuildModule(
        ModuleSyntax module,
        string chapter,
        IReadOnlyDictionary<string, string> moduleChapters)
    {
        var content = module.Content
            .Select(ToContentBlock)
            .ToList();

        var exits = new List<IrExit>();

        if (module.Exit is { } exit)
        {
            foreach (var branch in exit.Branches)
            {
                exits.Add(new IrExit(
                    exit.Character,
                    branch.Condition is null ? null : ToBoolExpr(branch.Condition),
                    branch.Label,
                    branch.Target,
                    branch.Effects
                        .Select(static effect => new IrAssignment(effect.Name, effect.Value))
                        .ToList(),
                    IsExternal(branch.Target, chapter, moduleChapters)));
            }
        }

        return new IrModule(
            module.Title,
            module.Title,
            module.Scene,
            chapter,
            content,
            exits);
    }

    private static bool IsExternal(
        string target,
        string sourceChapter,
        IReadOnlyDictionary<string, string> moduleChapters)
        => !string.Equals(target, "END", StringComparison.Ordinal) &&
           moduleChapters.TryGetValue(target, out var targetChapter) &&
           !string.Equals(targetChapter, sourceChapter, StringComparison.Ordinal);

    private static IrContentBlock ToContentBlock(ContentSyntax content)
        => content switch
        {
            NarrationSyntax narration => IrContentBlock.Narration(ToSegments(narration.Segments)),
            DialogueSyntax dialogue => IrContentBlock.Dialogue(dialogue.Speaker, ToSegments(dialogue.Segments)),
            FigureSyntax figure => IrContentBlock.Figure(figure.Path),
            _ => new IrContentBlock("unknown"),
        };

    private static IReadOnlyList<IrTextSegment> ToSegments(IReadOnlyList<TextSegmentSyntax> segments)
        => segments
            .Select(static segment => segment switch
            {
                TextLiteralSyntax literal => new IrTextSegment("text", Text: literal.Text),
                TextReferenceSyntax reference => new IrTextSegment("ref", Name: reference.Name),
                _ => new IrTextSegment("unknown"),
            })
            .ToList();

    private static IrBoolExpr ToBoolExpr(BoolExprSyntax expression)
        => expression switch
        {
            BoolLiteralSyntax literal => IrBoolExpr.Literal(literal.Value),
            BoolRefSyntax reference => IrBoolExpr.Ref(reference.Name),
            NotExprSyntax not => IrBoolExpr.Not(ToBoolExpr(not.Operand)),
            AndExprSyntax and => IrBoolExpr.And(ToBoolExpr(and.Left), ToBoolExpr(and.Right)),
            OrExprSyntax or => IrBoolExpr.Or(ToBoolExpr(or.Left), ToBoolExpr(or.Right)),
            _ => new IrBoolExpr("unknown"),
        };
}
