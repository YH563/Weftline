#pragma once

#include <cstddef>
#include <optional>
#include <string>
#include <vector>

#include "weftline/projects/project_loader.hpp"

namespace weftline::projects {

    /**
     * @brief 项目入口解析结果。
     *
     * default_entry：显式 @entry；否则全局入度最小、同分按源顺序取第一个模块。
     * candidates：除默认入口外的零入度模块；用于可达性、图角标和预览起点。
     */
    struct ProjectEntries
    {
        std::optional<std::u16string> default_entry;
        std::vector<std::u16string> candidates;
        bool is_explicit{false};
        std::size_t default_in_degree{0};
    };

    /**
     * @brief 按 .wstory 显式入口或全局最小入度解析默认入口和无入边候选入口。
     *
     * 同名模块只保留第一次出现，与 StoryGraph 的模块去重规则一致。
     */
    [[nodiscard]] ProjectEntries resolve_entries(const LoadedProject& project);

} // namespace weftline::projects
