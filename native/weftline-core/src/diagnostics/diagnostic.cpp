#include "weftline/diagnostics/diagnostic.hpp"

#include <algorithm>
#include <charconv>
#include <string>
#include <system_error>
#include <utility>

namespace weftline::diagnostics {
    namespace {

        /// @brief 把 UTF-16 code unit 格式化成十六进制。
        [[nodiscard]] std::u16string format_code_unit(char16_t value)
        {
            char buffer[8]{};
            const auto result = std::to_chars(
                buffer, buffer + sizeof(buffer), static_cast<unsigned>(value), 16);
            if (result.ec != std::errc{}) {
                return u"????";
            }

            std::u16string out;
            for (const char* cursor = buffer; cursor != result.ptr; ++cursor) {
                out.push_back(static_cast<char16_t>(*cursor));
            }
            return out;
        }

        /// @brief 把无符号整数转成 UTF-16 文本。
        [[nodiscard]] std::u16string to_u16string(std::size_t value)
        {
            const std::string text = std::to_string(value);
            return std::u16string(text.begin(), text.end());
        }

    } // namespace

    void DiagnosticBag::add(Diagnostic diagnostic)
    {
        items_.push_back(std::move(diagnostic));
    }

    void DiagnosticBag::error(std::string_view code, TextSpan span, std::u16string message)
    {
        add(Diagnostic{std::string(code), Severity::Error, std::move(message), span, std::nullopt});
    }

    void DiagnosticBag::warning(std::string_view code, TextSpan span, std::u16string message)
    {
        add(Diagnostic{std::string(code), Severity::Warning, std::move(message), span, std::nullopt});
    }

    void DiagnosticBag::info(std::string_view code, TextSpan span, std::u16string message)
    {
        add(Diagnostic{std::string(code), Severity::Info, std::move(message), span, std::nullopt});
    }

    bool DiagnosticBag::has_errors() const noexcept
    {
        return std::any_of(
            items_.begin(), items_.end(),
            [](const Diagnostic& diagnostic) { return diagnostic.severity == Severity::Error; });
    }

    Diagnostic make_unterminated_string(TextSpan span)
    {
        return Diagnostic{
            std::string(DiagnosticCode::UnterminatedStringLiteral),
            Severity::Error,
            u"字符串未闭合",
            span,
            std::nullopt,
        };
    }

    Diagnostic make_unexpected_character(char16_t character, TextSpan span)
    {
        std::u16string message = u"无法识别的字符 U+";
        message += format_code_unit(character);

        return Diagnostic{
            std::string(DiagnosticCode::UnknownLine),
            Severity::Error,
            std::move(message),
            span,
            std::nullopt,
        };
    }

    Diagnostic make_invalid_utf8(std::size_t byte_offset)
    {
        std::u16string message = u"无效 UTF-8：字节偏移 ";
        message += to_u16string(byte_offset);

        return Diagnostic{
            std::string(DiagnosticCode::InvalidUtf8),
            Severity::Error,
            std::move(message),
            TextSpan{},
            std::nullopt,
        };
    }

} // namespace weftline::diagnostics
