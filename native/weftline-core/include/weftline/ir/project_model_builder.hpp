#pragma once

#include "weftline/ir/project_model.hpp"
#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_loader.hpp"

namespace weftline::ir {

    /**
     * @brief 从已加载项目和项目索引构建源级语义模型。
     *
     * 项目名、概要和 @doc 来自 .wstory；章节/模块顺序和声明顺序来自 .weft。
     */
    [[nodiscard]] ProjectModel build_project_model(
        const projects::LoadedProject& project,
        const projects::ProjectIndex& index);

} // namespace weftline::ir
