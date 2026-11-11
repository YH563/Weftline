#pragma once

#include <string_view>
#include <vector>

#include "weftline/projects/project_index.hpp"

namespace weftline::projects {

    struct BindingCache;

    /**
     * @brief 合并所有 .weft 声明、建立项目索引，并逐文件绑定模块引用。
     *
     * 文件按传入顺序处理；同一文件内的模块重名由解析阶段负责，
     * 跨文件重名在这里报告。
     *
     * @param files 已解析的 .weft 文件。
     * @param wstory 可选的 .wstory 项目总览。
     * @param wstory_path .wstory 的路径；为空表示没有。
     * @param cache 可选的绑定缓存；符号签名不变时复用未变化文件的模块诊断。
     */
    [[nodiscard]] ProjectBindingResult bind(
        std::vector<ProjectFile> files,
        const syntax::WStoryFile* wstory,
        std::string_view wstory_path,
        BindingCache* cache = nullptr);

} // namespace weftline::projects
