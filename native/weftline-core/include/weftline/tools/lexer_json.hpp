#pragma once

#include <string_view>
#include <string>

namespace weftline::tools {

    /**
     * @brief 把 UTF-8 源码 dump 成 lexer/token JSON，供 weftc 调试使用。
     *
     * 这是 CLI 内部工具，不是面向宿主的 C ABI。
     */
    [[nodiscard]]
    std::string lexer_json(
        std::string_view source_utf8,
        std::string_view file_path);

} // namespace weftline::tools
