#include "weftline/generation/wstory_generator.hpp"

#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>
#include <utility>
#include <vector>

#include "weftline/source/text_utils.hpp"

namespace weftline::generation {
    namespace {

        using DescriptionMap = std::unordered_map<std::u16string, std::u16string>;

        // 保留作者写的描述；空描述也输出 []，让生成结果结构稳定。
        [[nodiscard]] std::u16string format_description(std::u16string_view text)
        {
            if (text.empty()) {
                return u"[]";
            }

            std::u16string result = u"[";
            result += text;
            result.push_back(u']');
            return result;
        }

        [[nodiscard]] std::u16string get_description(
            const DescriptionMap& descriptions, std::u16string_view name)
        {
            const auto found = descriptions.find(std::u16string(name));
            return found == descriptions.end() ? std::u16string{} : found->second;
        }

        // 从旧 .wstory 收集“名字 → 描述”，生成时只覆盖声明部分，描述原样保留。
        template <typename Entry>
        [[nodiscard]] DescriptionMap build_description_map(
            const std::vector<Entry>& entries,
            std::u16string Entry::*name,
            std::optional<syntax::TextBlock> Entry::*description)
        {
            DescriptionMap map;
            for (const auto& entry : entries) {
                const auto& value = entry.*description;
                map[entry.*name] = value ? value->text : std::u16string{};
            }
            return map;
        }

        // 输出一个声明分组；没有条目时整组省略。
        void write_group(
            std::u16string& builder,
            std::u16string_view group,
            std::u16string_view keyword,
            const std::vector<std::u16string>& names,
            const DescriptionMap& descriptions)
        {
            if (names.empty()) {
                return;
            }

            builder.push_back(u'\n');
            builder += u"    @";
            builder += group;
            builder.push_back(u'\n');

            for (const auto& name : names) {
                builder += u"        @";
                builder += keyword;
                builder.push_back(u' ');
                builder += name;
                builder.push_back(u' ');
                builder += format_description(get_description(descriptions, name));
                builder.push_back(u'\n');
            }
        }

    } // namespace

    // .wstory 生成器：声明以 .weft 为准，描述/概要/文档从 existing 继承。
    std::u16string generate(
        std::span<const syntax::WeftFile> weft_files,
        const syntax::WStoryFile* existing,
        std::u16string_view fallback_project_name)
    {
        std::vector<std::u16string> chapters;
        std::vector<std::pair<std::u16string, bool>> variables;
        std::vector<std::u16string> characters;
        std::vector<std::u16string> items;
        std::vector<std::u16string> scenes;

        std::unordered_set<std::u16string> seen_chapters;
        std::unordered_set<std::u16string> seen_variables;
        std::unordered_set<std::u16string> seen_characters;
        std::unordered_set<std::u16string> seen_items;
        std::unordered_set<std::u16string> seen_scenes;

        // 声明去重且保持首次出现顺序，与 ProjectBinder 的规则一致。
        for (const auto& weft : weft_files) {
            if (seen_chapters.insert(weft.title).second) {
                chapters.push_back(weft.title);
            }

            for (const auto& variable : weft.variables) {
                if (seen_variables.insert(variable.name).second) {
                    variables.emplace_back(variable.name, variable.value);
                }
            }

            for (const auto& character : weft.characters) {
                if (seen_characters.insert(character.name).second) {
                    characters.push_back(character.name);
                }
            }

            for (const auto& item : weft.items) {
                if (seen_items.insert(item.name).second) {
                    items.push_back(item.name);
                }
            }

            for (const auto& scene : weft.scenes) {
                if (seen_scenes.insert(scene.name).second) {
                    scenes.push_back(scene.name);
                }
            }
        }

        DescriptionMap chapter_descriptions;
        DescriptionMap character_descriptions;
        DescriptionMap item_descriptions;
        DescriptionMap scene_descriptions;
        DescriptionMap variable_descriptions;

        // 先取出旧描述，后面只改变量初始值和声明列表，不丢作者内容。
        if (existing != nullptr) {
            chapter_descriptions = build_description_map(
                existing->chapters,
                &syntax::WStoryChapter::title,
                &syntax::WStoryChapter::description);
            character_descriptions = build_description_map(
                existing->characters,
                &syntax::WStoryCharacter::name,
                &syntax::WStoryCharacter::description);
            item_descriptions = build_description_map(
                existing->items,
                &syntax::WStoryItem::name,
                &syntax::WStoryItem::description);
            scene_descriptions = build_description_map(
                existing->scenes,
                &syntax::WStoryScene::name,
                &syntax::WStoryScene::description);
            variable_descriptions = build_description_map(
                existing->variables,
                &syntax::WStoryVariable::name,
                &syntax::WStoryVariable::description);
        }

        std::u16string project_name;
        if (existing != nullptr
            && !source::trim(existing->project_name).empty()) {
            project_name = existing->project_name;
        } else {
            project_name = std::u16string(fallback_project_name);
        }

        std::u16string builder;
        builder += u"@project ";
        builder += project_name;
        builder.push_back(u'\n');

        if (existing != nullptr && existing->summary) {
            builder += u"    @summary ";
            builder += format_description(existing->summary->text);
            builder.push_back(u'\n');
        }

        write_group(
            builder, u"chapters", u"chapter", chapters, chapter_descriptions);

        if (existing != nullptr && !existing->docs.empty()) {
            builder.push_back(u'\n');
            builder += u"    @docs\n";
            for (const auto& doc : existing->docs) {
                builder += u"        @doc ";
                builder += doc.title;
                builder.push_back(u' ');
                builder += format_description(doc.body.text);
                builder.push_back(u'\n');
            }
        }

        write_group(
            builder, u"chars", u"char", characters, character_descriptions);
        write_group(
            builder, u"items", u"item", items, item_descriptions);
        write_group(
            builder, u"scenes", u"scene", scenes, scene_descriptions);

        if (!variables.empty()) {
            builder.push_back(u'\n');
            builder += u"    @vars\n";
            for (const auto& [name, value] : variables) {
                builder += u"        @var ";
                builder += name;
                builder.push_back(u' ');
                builder += value ? u"true" : u"false";
                builder.push_back(u' ');
                builder += format_description(
                    get_description(variable_descriptions, name));
                builder.push_back(u'\n');
            }
        }

        return builder;
    }

} // namespace weftline::generation
