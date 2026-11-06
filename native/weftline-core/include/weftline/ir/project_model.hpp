#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include "weftline/source/location.hpp"

namespace weftline::ir {

    /**
     * @brief 一条世界设定文档。
     */
    struct Doc
    {
        std::u16string title;
        std::u16string text;
    };

    /**
     * @brief 项目信息：名称、概要和设定文档。
     */
    struct ProjectInfo
    {
        std::u16string name;
        std::optional<std::u16string> summary;
        std::vector<Doc> docs;
    };

    /**
     * @brief 一个章节：标题、描述、所属模块和源码位置。
     */
    struct ProjectChapter
    {
        std::u16string id;
        std::u16string title;
        std::optional<std::u16string> description;
        std::vector<std::u16string> module_ids;
        source::SourceLocation source;
    };

    /**
     * @brief 一个全局变量声明。
     */
    struct ProjectVariable
    {
        std::u16string name;
        bool initial{};
        std::optional<std::u16string> description;
    };

    /**
     * @brief 一个角色声明。
     */
    struct ProjectCharacter
    {
        std::u16string name;
        std::optional<std::u16string> description;
    };

    /**
     * @brief 一个道具声明。
     */
    struct ProjectItem
    {
        std::u16string name;
        std::optional<std::u16string> description;
    };

    /**
     * @brief 一个场景声明。
     */
    struct ProjectScene
    {
        std::u16string name;
        std::optional<std::u16string> description;
    };

    /**
     * @brief 项目级声明，保持源文件中的出现顺序。
     */
    struct ProjectSymbols
    {
        std::vector<ProjectVariable> variables;
        std::vector<ProjectCharacter> characters;
        std::vector<ProjectItem> items;
        std::vector<ProjectScene> scenes;
    };

    /**
     * @brief 源级语义模型：项目信息、章节和声明。
     */
    struct ProjectModel
    {
        ProjectInfo project;
        std::vector<ProjectChapter> chapters;
        ProjectSymbols symbols;
    };

} // namespace weftline::ir
