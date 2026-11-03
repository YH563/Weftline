using Weftline.Language.Binding;
using Weftline.Language.Diagnostics;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language.Projects;

/// <summary>
/// 项目级绑定结果：索引与按文件归属的诊断。
/// Project-level binding result: the index and per-file diagnostics.
/// </summary>
public sealed record ProjectBindingResult(
    ProjectIndex Index,
    IReadOnlyList<ProjectDiagnostic> Diagnostics);

/// <summary>
/// 合并所有 `.weft` 声明，建立项目索引，并逐文件绑定模块引用。
/// Merges all `.weft` declarations, builds the project index, and binds module
/// references per file.
/// </summary>
public static class ProjectBinder
{
    public static ProjectBindingResult Bind(
        IReadOnlyList<ProjectFile> files,
        WStoryFileSyntax? wstory,
        string? wstoryPath)
    {
        var diagnostics = new List<ProjectDiagnostic>();
        var symbols = new SymbolTable();
        var chapters = new Dictionary<string, ChapterDefinition>(StringComparer.Ordinal);
        var modules = new Dictionary<string, ModuleDefinition>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            DeclareChapter(file, chapters, diagnostics);
            DeclareSymbols(file, symbols, diagnostics);
            DeclareModules(file, file.Ast.Title, modules, diagnostics);
        }

        if (wstory is not null)
        {
            var wstoryBag = new DiagnosticBag();
            Binder.Bind(wstory, wstoryBag, wstoryPath ?? string.Empty);
            diagnostics.AddRange(wstoryBag.Select(
                diagnostic => new ProjectDiagnostic(wstoryPath ?? string.Empty, diagnostic)));
        }

        foreach (var file in files)
        {
            foreach (var module in file.Ast.Modules)
            {
                var bag = new DiagnosticBag();
                Binder.BindModule(module, symbols, bag);
                diagnostics.AddRange(bag.Select(
                    diagnostic => new ProjectDiagnostic(file.FilePath, diagnostic)));
            }
        }

        return new ProjectBindingResult(
            new ProjectIndex(symbols.Symbols, chapters, modules, files),
            diagnostics);
    }

    private static void DeclareChapter(
        ProjectFile file,
        Dictionary<string, ChapterDefinition> chapters,
        List<ProjectDiagnostic> diagnostics)
    {
        var definition = new ChapterDefinition(
            file.Ast.Title,
            file.FilePath,
            file.Ast.Span,
            file.Ast.TitleSpan);

        if (chapters.TryGetValue(file.Ast.Title, out var existing))
        {
            diagnostics.Add(new ProjectDiagnostic(
                file.FilePath,
                new Diagnostic(
                    DiagnosticCode.DuplicateChapterTitle,
                    DiagnosticSeverity.Error,
                    $"章节标题重复：{file.Ast.Title}",
                    file.Ast.TitleSpan),
                new SourceLocation(existing.FilePath, existing.TitleSpan)));
            return;
        }

        chapters[file.Ast.Title] = definition;
    }

    private static void DeclareSymbols(
        ProjectFile file,
        SymbolTable symbols,
        List<ProjectDiagnostic> diagnostics)
    {
        foreach (var variable in file.Ast.Variables)
        {
            Declare(file, symbols, diagnostics, variable.Name, SymbolKind.Variable, variable.Value, variable.Span);
        }

        foreach (var character in file.Ast.Characters)
        {
            Declare(file, symbols, diagnostics, character.Name, SymbolKind.Character, null, character.Span);
        }

        foreach (var item in file.Ast.Items)
        {
            Declare(file, symbols, diagnostics, item.Name, SymbolKind.Item, null, item.Span);
        }

        foreach (var scene in file.Ast.Scenes)
        {
            Declare(file, symbols, diagnostics, scene.Name, SymbolKind.Scene, null, scene.Span);
        }
    }

    private static void Declare(
        ProjectFile file,
        SymbolTable symbols,
        List<ProjectDiagnostic> diagnostics,
        string name,
        SymbolKind kind,
        bool? value,
        TextSpan span)
    {
        var entry = new SymbolEntry(name, kind, new SourceLocation(file.FilePath, span), value);

        if (symbols.TryDeclare(entry, out var existing))
        {
            return;
        }

        var code = existing!.Kind == kind
            ? DuplicateCode(kind)
            : DiagnosticCode.DuplicateSymbol;

        var message = existing.Kind == kind
            ? $"全局{KindName(kind)}重复声明：{name}"
            : $"符号名已被声明为{KindName(existing.Kind)}：{name}";

        diagnostics.Add(new ProjectDiagnostic(
            file.FilePath,
            new Diagnostic(code, DiagnosticSeverity.Error, message, span),
            existing.Location));
    }

    private static void DeclareModules(
        ProjectFile file,
        string chapter,
        Dictionary<string, ModuleDefinition> modules,
        List<ProjectDiagnostic> diagnostics)
    {
        foreach (var module in file.Ast.Modules)
        {
            var definition = new ModuleDefinition(
                module.Title,
                chapter,
                file.FilePath,
                module.Span,
                module.TitleSpan,
                module.Scene);

            if (!modules.TryGetValue(module.Title, out var existing))
            {
                modules[module.Title] = definition;
                continue;
            }

            if (string.Equals(existing.FilePath, file.FilePath, StringComparison.Ordinal))
            {
                continue;
            }

            diagnostics.Add(new ProjectDiagnostic(
                file.FilePath,
                new Diagnostic(
                    DiagnosticCode.DuplicateModuleTitle,
                    DiagnosticSeverity.Error,
                    $"模块 ID 重复：{module.Title}",
                    module.TitleSpan),
                new SourceLocation(existing.FilePath, existing.TitleSpan)));
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
