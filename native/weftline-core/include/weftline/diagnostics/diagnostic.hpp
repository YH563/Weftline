#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#include "weftline/source/location.hpp"
#include "weftline/source/source_text.hpp"

namespace weftline::diagnostics {

    using source::SourceLocation;
    using source::TextSpan;

    /**
     * @brief 诊断严重级别。
     */
    enum class Severity : std::uint8_t {
        Info,
        Warning,
        Error,
    };

    /**
     * @brief 返回稳定的级别名称，用于 JSON 输出。
     */
    [[nodiscard]]
    constexpr std::string_view severity_name(Severity severity) noexcept
    {
        switch (severity) {
            using enum Severity;
            case Info:
                return "info";
            case Warning:
                return "warning";
            case Error:
                return "error";
        }

        return "error";
    }

    /**
     * @brief 一条编译器诊断。
     *
     * code 是稳定的诊断码（例如 WFT1001），message 是给用户看的文本，
     * span 指向出错位置；related 可选，用于指向首次声明等相关位置。
     */
    struct Diagnostic
    {
        std::string code;
        Severity severity{Severity::Error};
        std::u16string message;
        TextSpan span;
        std::optional<SourceLocation> related;
    };

    /**
     * @brief 诊断收集器。
     *
     * 解析器和后续校验阶段写入同一个 bag；用户输入错误不抛异常。
     */
    class DiagnosticBag
    {
    public:
        /// @brief 添加一条已经构造好的诊断。
        void add(Diagnostic diagnostic);

        /// @brief 添加一条错误诊断。
        void error(std::string_view code, TextSpan span, std::u16string message);

        /// @brief 添加一条警告诊断。
        void warning(std::string_view code, TextSpan span, std::u16string message);

        /// @brief 添加一条信息诊断。
        void info(std::string_view code, TextSpan span, std::u16string message);

        /// @brief 是否存在 error 级别的诊断。
        [[nodiscard]] bool has_errors() const noexcept;

        /// @brief 是否没有任何诊断。
        [[nodiscard]] bool empty() const noexcept
        {
            return items_.empty();
        }

        /// @brief 诊断数量。
        [[nodiscard]] std::size_t size() const noexcept
        {
            return items_.size();
        }

        /// @brief 只读诊断列表。
        [[nodiscard]] const std::vector<Diagnostic>& items() const noexcept
        {
            return items_;
        }

        [[nodiscard]] auto begin() const noexcept
        {
            return items_.begin();
        }

        [[nodiscard]] auto end() const noexcept
        {
            return items_.end();
        }

    private:
        std::vector<Diagnostic> items_;
    };

    /**
     * @brief 诊断码。
     *
     * 1xxx 词法与语法，2xxx 声明与模块，3xxx 内容与出口，4xxx 项目与图。
     * WFT1010 / WFT1011 是 C++ 词法器阶段的扩展码。
     */
    namespace DiagnosticCode {
        // 词法与语法
        inline constexpr std::string_view UnknownLine = "WFT1001";
        inline constexpr std::string_view UnterminatedTextBlock = "WFT1002";
        inline constexpr std::string_view MalformedDirective = "WFT1003";
        inline constexpr std::string_view MalformedExitBranch = "WFT1004";
        inline constexpr std::string_view InvalidTitleCharacter = "WFT1005";
        inline constexpr std::string_view UnterminatedReference = "WFT1006";
        inline constexpr std::string_view UnterminatedStringLiteral = "WFT1010";
        inline constexpr std::string_view InvalidUtf8 = "WFT1011";

        // 声明与模块
        inline constexpr std::string_view VarOutsideGlobals = "WFT2001";
        inline constexpr std::string_view MultipleGlobals = "WFT2002";
        inline constexpr std::string_view GlobalsNotAtFileHeader = "WFT2003";
        inline constexpr std::string_view DuplicateGlobalVariable = "WFT2004";
        inline constexpr std::string_view DuplicateModuleTitle = "WFT2005";
        inline constexpr std::string_view HeaderAfterContent = "WFT2006";
        inline constexpr std::string_view EmptyTitle = "WFT2007";
        inline constexpr std::string_view UndeclaredCharacter = "WFT2008";
        inline constexpr std::string_view DuplicateCharacter = "WFT2009";
        inline constexpr std::string_view DuplicateItem = "WFT2010";
        inline constexpr std::string_view DuplicateSymbol = "WFT2011";
        inline constexpr std::string_view SetInProjectFile = "WFT2012";
        inline constexpr std::string_view SetInContent = "WFT2013";
        inline constexpr std::string_view UndeclaredReference = "WFT2014";
        inline constexpr std::string_view DuplicateScene = "WFT2015";
        inline constexpr std::string_view MalformedScene = "WFT2016";
        inline constexpr std::string_view MissingChapterTitle = "WFT2017";
        inline constexpr std::string_view MultipleChapterTitle = "WFT2018";
        inline constexpr std::string_view DuplicateChapterTitle = "WFT2019";

        // 内容与出口
        inline constexpr std::string_view MissingExitBlock = "WFT3001";
        inline constexpr std::string_view MissingFallback = "WFT3002";
        inline constexpr std::string_view ModeMismatch = "WFT3003";
        inline constexpr std::string_view InvalidBranchLabel = "WFT3004";
        inline constexpr std::string_view ContentAfterExit = "WFT3005";
        inline constexpr std::string_view UndeclaredConditionVariable = "WFT3006";
        inline constexpr std::string_view UndeclaredVariable = "WFT3007";
        inline constexpr std::string_view InvalidCondition = "WFT3008";
        inline constexpr std::string_view MalformedEffect = "WFT3009";
        inline constexpr std::string_view DuplicateEffectAssignment = "WFT3010";

        // 项目与图
        inline constexpr std::string_view TargetNotFound = "WFT4001";
        inline constexpr std::string_view InvalidEnd = "WFT4002";
        inline constexpr std::string_view EmptyProject = "WFT4003";
        inline constexpr std::string_view UnreachableModule = "WFT4004";
        inline constexpr std::string_view PathCannotReachEnd = "WFT4005";
        inline constexpr std::string_view ExternalTargetInIsolation = "WFT4006";
        inline constexpr std::string_view MissingAsset = "WFT4007";
        inline constexpr std::string_view UnusedGlobalVariable = "WFT4008";
    } // namespace DiagnosticCode

    /**
     * @brief 构造“字符串未闭合”诊断。
     */
    [[nodiscard]] Diagnostic make_unterminated_string(TextSpan span);

    /**
     * @brief 构造“无法识别字符”诊断。
     */
    [[nodiscard]] Diagnostic make_unexpected_character(char16_t character, TextSpan span);

    /**
     * @brief 构造“无效 UTF-8”诊断。
     */
    [[nodiscard]] Diagnostic make_invalid_utf8(std::size_t byte_offset);

} // namespace weftline::diagnostics
