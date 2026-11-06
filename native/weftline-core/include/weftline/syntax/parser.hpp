#pragma once

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::syntax {

    /**
     * @brief 解析一个脚本文件（.weft 或 .wstory）。
     *
     * 根据文件名或首个非空指令选择解析入口；诊断写入 diagnostics。
     * 即使存在错误，也会返回当前可用的部分 AST。
     */
    [[nodiscard]] ScriptFile parse(
        const source::SourceText& source,
        diagnostics::DiagnosticBag& diagnostics);

} // namespace weftline::syntax
