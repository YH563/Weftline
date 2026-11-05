using Weftline.Language.Projects;
using Weftline.Language.Source;
using Weftline.Language.Syntax;

namespace Weftline.Language.Ir;

/// <summary>
/// 从已加载项目和项目索引构建源级语义模型。
/// Builds the source-level semantic model from a loaded project and project index.
/// </summary>
public static class ProjectModelBuilder
{
    public static ProjectModel Build(LoadedProject project, ProjectIndex index)
    {
        var wstory = project.WStory;

        var projectInfo = new IrProjectInfo(
            string.IsNullOrWhiteSpace(wstory?.ProjectName)
                ? FolderName(project.Folder)
                : wstory!.ProjectName,
            wstory?.Summary?.Text,
            wstory?.Docs
                .Select(static doc => new IrDoc(doc.Title, doc.Body.Text))
                .ToList() ?? []);

        var chapterDescriptions = BuildDescriptionMap(
            wstory?.Chapters.Select(static chapter => (chapter.Title, chapter.Description)));

        var variableDescriptions = BuildDescriptionMap(
            wstory?.Variables.Select(static variable => (variable.Name, variable.Description)));

        var characterDescriptions = BuildDescriptionMap(
            wstory?.Characters.Select(static character => (character.Name, character.Description)));

        var itemDescriptions = BuildDescriptionMap(
            wstory?.Items.Select(static item => (item.Name, item.Description)));

        var sceneDescriptions = BuildDescriptionMap(
            wstory?.Scenes.Select(static scene => (scene.Name, scene.Description)));

        return new ProjectModel(
            projectInfo,
            BuildChapters(project, index, chapterDescriptions),
            BuildSymbols(
                project,
                variableDescriptions,
                characterDescriptions,
                itemDescriptions,
                sceneDescriptions));
    }

    private static IReadOnlyList<ProjectChapter> BuildChapters(
        LoadedProject project,
        ProjectIndex index,
        IReadOnlyDictionary<string, string?> descriptions)
    {
        var chapters = new List<ChapterBuilder>();
        var chapterIndexes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in project.WeftFiles)
        {
            var title = file.Ast.Title;

            if (!chapterIndexes.TryGetValue(title, out var position))
            {
                position = chapters.Count;
                chapterIndexes[title] = position;

                var source = index.Chapters.TryGetValue(title, out var definition)
                    ? new SourceLocation(definition.FilePath, definition.TitleSpan)
                    : new SourceLocation(file.FilePath, file.Ast.TitleSpan);

                chapters.Add(new ChapterBuilder(
                    title,
                    GetDescription(descriptions, title),
                    source));
            }

            foreach (var module in file.Ast.Modules)
            {
                chapters[position].ModuleIds.Add(module.Title);
            }
        }

        return chapters
            .Select(static chapter => new ProjectChapter(
                chapter.Id,
                chapter.Id,
                chapter.Description,
                chapter.ModuleIds,
                chapter.Source))
            .ToList();
    }

    private sealed class ChapterBuilder
    {
        public ChapterBuilder(string id, string? description, SourceLocation source)
        {
            Id = id;
            Description = description;
            Source = source;
        }

        public string Id { get; }

        public string? Description { get; }

        public SourceLocation Source { get; }

        public List<string> ModuleIds { get; } = [];
    }

    private static ProjectSymbols BuildSymbols(
        LoadedProject project,
        IReadOnlyDictionary<string, string?> variableDescriptions,
        IReadOnlyDictionary<string, string?> characterDescriptions,
        IReadOnlyDictionary<string, string?> itemDescriptions,
        IReadOnlyDictionary<string, string?> sceneDescriptions)
    {
        var variables = new List<ProjectVariable>();
        var characters = new List<ProjectCharacter>();
        var items = new List<ProjectItem>();
        var scenes = new List<ProjectScene>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in project.WeftFiles)
        {
            foreach (var variable in file.Ast.Variables)
            {
                if (seen.Add(variable.Name))
                {
                    variables.Add(new ProjectVariable(
                        variable.Name,
                        variable.Value,
                        GetDescription(variableDescriptions, variable.Name)));
                }
            }

            foreach (var character in file.Ast.Characters)
            {
                if (seen.Add(character.Name))
                {
                    characters.Add(new ProjectCharacter(
                        character.Name,
                        GetDescription(characterDescriptions, character.Name)));
                }
            }

            foreach (var item in file.Ast.Items)
            {
                if (seen.Add(item.Name))
                {
                    items.Add(new ProjectItem(
                        item.Name,
                        GetDescription(itemDescriptions, item.Name)));
                }
            }

            foreach (var scene in file.Ast.Scenes)
            {
                if (seen.Add(scene.Name))
                {
                    scenes.Add(new ProjectScene(
                        scene.Name,
                        GetDescription(sceneDescriptions, scene.Name)));
                }
            }
        }

        return new ProjectSymbols(variables, characters, items, scenes);
    }

    private static Dictionary<string, string?> BuildDescriptionMap(
        IEnumerable<(string Name, TextBlockSyntax? Description)>? entries)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (entries is null)
        {
            return map;
        }

        foreach (var (name, description) in entries)
        {
            map.TryAdd(name, description?.Text);
        }

        return map;
    }

    private static string? GetDescription(
        IReadOnlyDictionary<string, string?> descriptions,
        string name)
        => descriptions.TryGetValue(name, out var description) ? description : null;

    private static string FolderName(string folder)
        => Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));
}
