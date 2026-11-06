#pragma once

#include <optional>
#include <string>
#include <vector>

#include "weftline/projects/project_index.hpp"
#include "weftline/projects/project_snapshot.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::projects {

    class CompilationCache;

    /**
     * @brief 一个已加载的项目：一个 .wstory 和若干 .weft。
     */
    struct LoadedProject
    {
        std::string folder;
        std::optional<syntax::WStoryFile> wstory;
        std::string wstory_path;
        std::vector<ProjectFile> weft_files;
        std::vector<ProjectDiagnostic> diagnostics;
    };

    /**
     * @brief 从快照加载项目。
     *
     * 只消费 snapshot.documents 中提供的文件，不访问磁盘。
     * 顶层 .weft 按路径排序；.wstory 取排序后的第一个。
     * cache 非空时，文本未变化的文件复用已解析 AST。
     */
    [[nodiscard]] LoadedProject load(
        const ProjectSnapshot& snapshot,
        CompilationCache* cache = nullptr);

} // namespace weftline::projects
