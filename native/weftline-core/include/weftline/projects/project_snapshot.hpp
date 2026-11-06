#pragma once

#include <cstdint>
#include <string>
#include <unordered_map>
#include <utility>

namespace weftline::projects {

    /**
     * @brief 一次项目编译使用的不可变快照。
     *
     * documents 是路径 → UTF-8 文本；core 不直接读磁盘，由宿主提供文档。
     */
    struct ProjectSnapshot
    {
        std::string folder;
        std::int64_t version{0};
        std::unordered_map<std::string, std::string> documents;

        /// @brief 创建没有内存文档的磁盘快照。
        [[nodiscard]] static ProjectSnapshot from_folder(
            std::string folder, std::int64_t version = 0)
        {
            return ProjectSnapshot{std::move(folder), version, {}};
        }
    };

} // namespace weftline::projects
