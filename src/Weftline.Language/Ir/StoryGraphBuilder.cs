using Weftline.Language.Projects;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language.Ir;

/// <summary>
/// 从项目 AST 与 ProjectModel 构建语义 StoryGraph。
/// Builds the semantic StoryGraph from project ASTs and the ProjectModel.
/// </summary>
public static class StoryGraphBuilder
{
    public static StoryGraph Build(LoadedProject project, ProjectIndex index)
        => Build(project, ProjectModelBuilder.Build(project, index));

    public static StoryGraph Build(LoadedProject project, ProjectIndex index, string? entry)
        => Build(project, ProjectModelBuilder.Build(project, index), entry);

    public static StoryGraph Build(
        LoadedProject project,
        ProjectModel model,
        string? entry = null)
    {
        var modules = new List<IrModule>();
        var seenModuleIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in project.WeftFiles)
        {
            foreach (var module in file.Ast.Modules)
            {
                if (!seenModuleIds.Add(module.Title))
                {
                    continue;
                }

                modules.Add(BuildModule(file.FilePath, file.Ast.Title, module));
            }
        }

        var globals = model.Symbols.Variables
            .Select(static variable => new IrGlobal(variable.Name, variable.Initial))
            .ToList();

        return new StoryGraph(
            2,
            "END",
            entry ?? modules.FirstOrDefault()?.Id,
            globals,
            modules);
    }

    private static IrModule BuildModule(
        string filePath,
        string chapterId,
        ModuleSyntax module)
    {
        var content = module.Content
            .Select(ToContentBlock)
            .ToList();

        var exit = module.Exit is { } exitSyntax
            ? new IrExitBlock(
                exitSyntax.IsChoiceMode ? ExitMode.Choice : ExitMode.Auto,
                exitSyntax.Character,
                exitSyntax.Branches
                    .Select(branch => new IrExitBranch(
                        branch.Condition is null ? null : ToBoolExpr(branch.Condition),
                        branch.Label,
                        branch.Target,
                        branch.Effects
                            .Select(static effect => new IrAssignment(effect.Name, effect.Value))
                            .ToList(),
                        new SourceLocation(filePath, branch.Span)))
                    .ToList(),
                new SourceLocation(filePath, exitSyntax.Span))
            : null;

        return new IrModule(
            module.Title,
            chapterId,
            module.Scene,
            content,
            exit,
            new SourceLocation(filePath, module.TitleSpan));
    }

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
