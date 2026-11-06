#pragma once

#include <string>
#include <string_view>

#include "weftline/binding/symbol_table.hpp"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::binding {

    /**
     * @brief 解析一个 .weft 文件的符号引用。
     *
     * 只检查语法树中的引用；不修改 AST。
     */
    void bind(
        const syntax::WeftFile& file,
        diagnostics::DiagnosticBag& diagnostics,
        std::string_view file_path);

    /**
     * @brief 解析一个 .wstory 文件的重复声明。
     */
    void bind(
        const syntax::WStoryFile& file,
        diagnostics::DiagnosticBag& diagnostics,
        std::string_view file_path);

    /**
     * @brief 解析单个模块中的对话、引用、条件和效果变量。
     */
    void bind_module(
        const syntax::Module& module,
        SymbolTable& symbols,
        diagnostics::DiagnosticBag& diagnostics);

    /**
     * @brief 声明一个符号并处理重复诊断。
     */
    bool declare(
        SymbolTable& symbols,
        std::u16string name,
        SymbolKind kind,
        std::optional<bool> value,
        source::TextSpan span,
        std::string_view file_path,
        diagnostics::DiagnosticBag& diagnostics);

    /// @brief 返回某个符号种类的重复声明诊断码。
    [[nodiscard]] std::string_view duplicate_code(SymbolKind kind) noexcept;

    /// @brief 返回符号种类的中文名。
    [[nodiscard]] std::u16string kind_name(SymbolKind kind);

} // namespace weftline::binding
