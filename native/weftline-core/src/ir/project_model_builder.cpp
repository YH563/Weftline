#include "weftline/ir/project_model_builder.hpp"

#include <algorithm>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

#include "weftline/source/source_text.hpp"
#include "weftline/source/text_utils.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::ir {
    namespace {

        using Description = std::optional<std::u16string>;
        using DescriptionMap = std::unordered_map<std::u16string, Description>;

        [[nodiscard]] std::u16string folder_name(std::string_view folder)
        {
            std::size_t end = folder.size();
            while (end > 0 && (folder[end - 1] == '/' || folder[end - 1] == '\\')) {
                --end;
            }

            std::size_t start = 0;
            for (std::size_t index = 0; index < end; ++index) {
                if (folder[index] == '/' || folder[index] == '\\') {
                    start = index + 1;
                }
            }

            return source::utf8_to_utf16(std::string(folder.substr(start, end - start)));
        }

        template <typename Entry>
        [[nodiscard]] DescriptionMap build_description_map(
            const std::vector<Entry>& entries,
            std::u16string Entry::*name,
            std::optional<syntax::TextBlock> Entry::*description)
        {
            DescriptionMap map;
            for (const auto& entry : entries) {
                const auto& value = entry.*description;
                map.try_emplace(
                    entry.*name,
                    value ? Description{value->text} : Description{});
            }
            return map;
        }

        [[nodiscard]] Description get_description(
            const DescriptionMap& descriptions, std::u16string_view name)
        {
            const auto found = descriptions.find(std::u16string(name));
            return found == descriptions.end() ? Description{} : found->second;
        }

        [[nodiscard]] std::vector<ProjectChapter> build_chapters(
            const projects::LoadedProject& project,
            const projects::ProjectIndex& index,
            const DescriptionMap& descriptions)
        {
            std::vector<ProjectChapter> chapters;
            std::unordered_map<std::u16string, std::size_t> positions;

            for (const auto& file : project.weft_files) {
                const auto& title = file.ast->title;

                std::size_t position = 0;
                const auto found = positions.find(title);
                if (found == positions.end()) {
                    position = chapters.size();
                    positions.emplace(title, position);

                    source::SourceLocation location;
                    const auto definition = index.chapters.find(title);
                    if (definition != index.chapters.end()) {
                        location = source::SourceLocation{
                            definition->second.file_path,
                            definition->second.title_span};
                    } else {
                        location = source::SourceLocation{
                            file.file_path, file.ast->title_span};
                    }

                    chapters.push_back(ProjectChapter{
                        title,
                        title,
                        get_description(descriptions, title),
                        {},
                        std::move(location),
                    });
                } else {
                    position = found->second;
                }

                for (const auto& module : file.ast->modules) {
                    chapters[position].module_ids.push_back(module.title);
                }
            }

            return chapters;
        }

        [[nodiscard]] ProjectSymbols build_symbols(
            const projects::LoadedProject& project,
            const DescriptionMap& variable_descriptions,
            const DescriptionMap& character_descriptions,
            const DescriptionMap& item_descriptions,
            const DescriptionMap& scene_descriptions)
        {
            ProjectSymbols symbols;
            std::unordered_set<std::u16string> seen;

            for (const auto& file : project.weft_files) {
                for (const auto& variable : file.ast->variables) {
                    if (seen.insert(variable.name).second) {
                        symbols.variables.push_back(ProjectVariable{
                            variable.name,
                            variable.value,
                            get_description(variable_descriptions, variable.name),
                        });
                    }
                }

                for (const auto& character : file.ast->characters) {
                    if (seen.insert(character.name).second) {
                        symbols.characters.push_back(ProjectCharacter{
                            character.name,
                            get_description(character_descriptions, character.name),
                        });
                    }
                }

                for (const auto& item : file.ast->items) {
                    if (seen.insert(item.name).second) {
                        symbols.items.push_back(ProjectItem{
                            item.name,
                            get_description(item_descriptions, item.name),
                        });
                    }
                }

                for (const auto& scene : file.ast->scenes) {
                    if (seen.insert(scene.name).second) {
                        symbols.scenes.push_back(ProjectScene{
                            scene.name,
                            get_description(scene_descriptions, scene.name),
                        });
                    }
                }
            }

            return symbols;
        }

    } // namespace

    ProjectModel build_project_model(
        const projects::LoadedProject& project,
        const projects::ProjectIndex& index)
    {
        const auto* wstory = project.wstory ? &*project.wstory : nullptr;

        std::u16string project_name;
        if (wstory != nullptr && !source::trim(wstory->project_name).empty()) {
            project_name = wstory->project_name;
        } else {
            project_name = folder_name(project.folder);
        }

        std::optional<std::u16string> summary;
        std::vector<Doc> docs;

        if (wstory != nullptr) {
            if (wstory->summary) {
                summary = wstory->summary->text;
            }
            for (const auto& doc : wstory->docs) {
                docs.push_back(Doc{doc.title, doc.body.text});
            }
        }

        DescriptionMap chapter_descriptions;
        DescriptionMap variable_descriptions;
        DescriptionMap character_descriptions;
        DescriptionMap item_descriptions;
        DescriptionMap scene_descriptions;

        if (wstory != nullptr) {
            chapter_descriptions = build_description_map(
                wstory->chapters,
                &syntax::WStoryChapter::title,
                &syntax::WStoryChapter::description);
            variable_descriptions = build_description_map(
                wstory->variables,
                &syntax::WStoryVariable::name,
                &syntax::WStoryVariable::description);
            character_descriptions = build_description_map(
                wstory->characters,
                &syntax::WStoryCharacter::name,
                &syntax::WStoryCharacter::description);
            item_descriptions = build_description_map(
                wstory->items,
                &syntax::WStoryItem::name,
                &syntax::WStoryItem::description);
            scene_descriptions = build_description_map(
                wstory->scenes,
                &syntax::WStoryScene::name,
                &syntax::WStoryScene::description);
        }

        return ProjectModel{
            ProjectInfo{
                std::move(project_name),
                std::move(summary),
                std::move(docs),
            },
            build_chapters(project, index, chapter_descriptions),
            build_symbols(
                project,
                variable_descriptions,
                character_descriptions,
                item_descriptions,
                scene_descriptions),
        };
    }

} // namespace weftline::ir
