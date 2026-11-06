#pragma once

#include <string>
#include <string_view>

namespace weftline::api {

    /**
     * @brief 处理 weft_compile_json 的请求 JSON。
     *
     * 当前实现只提取 filePath / text 两个字段，属于占位解析；
     * 后续可替换为正式 JSON 解析器而不改变外部契约。
     */
    [[nodiscard]]
    std::string compile_to_json(std::string_view request_json);

    /**
     * @brief 把 UTF-8 源码编译成响应 JSON。
     *
     * @param source_utf8 源文件内容（UTF-8）。
     * @param file_path 源文件路径，仅用于回显与诊断定位。
     */
    [[nodiscard]]
    std::string compile_source_to_json(
        std::string_view source_utf8,
        std::string_view file_path);

    /**
     * @brief 构造统一格式的错误 JSON。
     */
    [[nodiscard]]
    std::string error_json(std::string_view message);

} // namespace weftline::api
