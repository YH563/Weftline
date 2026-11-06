#pragma once

#include <cstdint>
#include <optional>
#include <string_view>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::syntax {

    /**
     * @brief 解析一个布尔条件表达式。
     *
     * @param text 条件内部文本（不含最外层括号）。
     * @param offset text 在源码中的绝对起始位置，用于诊断。
     * @param diagnostics 诊断收集器。
     * @return 解析出的表达式；空文本或无法解析时返回 nullopt。
     */
    [[nodiscard]] std::optional<BoolExpr> parse_condition(
        std::u16string_view text,
        std::int32_t offset,
        diagnostics::DiagnosticBag& diagnostics);

} // namespace weftline::syntax
