#pragma once

#include <optional>
#include <string>
#include <vector>

#include "weftline/projects/project_loader.hpp"

namespace weftline::projects {

    /**
     * @brief 项目入口解析结果。
     *
     * default_entry：第一个文件的第一个模块；语言没有显式入口声明。
     * candidates：除默认入口外，没有入边的模块；用于可达性、图角标和预览起点。
     */
    struct ProjectEntries
    {
        std::optional<std::u16string> default_entry;
        std::vector<std::u16string> candidates;
    };

    /**
     * @brief 按文件顺序解析默认入口和无入边候选入口。
     *
     * 同名模块只保留第一次出现，与 StoryGraph 的模块去重规则一致。
     */
    [[nodiscard]] ProjectEntries resolve_entries(const LoadedProject& project);

} // namespace weftline::projects
