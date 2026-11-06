#pragma once

#include <string>

#include "weftline/source/source_text.hpp"

namespace weftline::source {

    /**
     * @brief 带文件路径的源码位置。
     */
    struct SourceLocation
    {
        std::string file_path;
        TextSpan span;
    };

} // namespace weftline::source
