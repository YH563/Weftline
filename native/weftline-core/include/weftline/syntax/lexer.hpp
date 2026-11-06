#pragma once

#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/syntax_facts.hpp"
#include "weftline/syntax/token.hpp"

namespace weftline::syntax {

    /**
     * @brief 一次词法分析的结果。
     *
     * tokens 始终以 EndOfFile 结尾；diagnostics 记录无法识别的字符和未闭合字符串。
     */
    struct LexResult
    {
        std::vector<Token> tokens;
        std::vector<diagnostics::Diagnostic> diagnostics;
    };

    /**
     * @brief 行/字符级扫描器。
     *
     * 只负责把源码切成 token，不构建 AST；构造后调用 tokenize()。
     * Lexer 只持有 SourceText 的引用，调用期间 source 必须保持有效。
     */
    class Lexer
    {
    public:
        explicit Lexer(const source::SourceText& source) noexcept
            : source_(source){}

        /**
         * @brief 扫描整个源码，返回 token 与诊断。
         */
        [[nodiscard]]
        LexResult tokenize() const;

    private:
        const source::SourceText& source_;
    };

} // namespace weftline::syntax
