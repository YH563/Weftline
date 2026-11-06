#pragma once

#include <string>

#include "weftline/syntax/ast.hpp"

namespace weftline::syntax {

    /**
     * @brief 以缩进树的形式打印 AST（UTF-16）。
     *
     * 与 C# 的 AstPrinter 输出保持一致，便于迁移期逐字对比。
     */
    [[nodiscard]]
    std::u16string print_ast(const ScriptFile& file);

    /**
     * @brief 以 UTF-8 返回 AST 文本树，供 CLI/日志使用。
     */
    [[nodiscard]]
    std::string print_ast_utf8(const ScriptFile& file);

} // namespace weftline::syntax
