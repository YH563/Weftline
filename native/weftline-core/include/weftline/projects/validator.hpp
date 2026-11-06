#pragma once

#include <vector>

#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"

namespace weftline::projects {

    /**
     * @brief 项目级图校验：目标存在、入口、可达性、能否到达 END。
     */
    [[nodiscard]] std::vector<ProjectDiagnostic> validate(
        const LoadedProject& project,
        const ProjectIndex& index);

} // namespace weftline::projects
