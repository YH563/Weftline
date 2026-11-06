#pragma once

#include <string_view>

#include "weftline/source/text_utils.hpp"

namespace weftline::syntax {

    using source::is_space;

    /// @brief ASCII 字母判断。
    [[nodiscard]] constexpr bool is_ascii_letter(char16_t value) noexcept
    {
        return (value >= u'a' && value <= u'z') || (value >= u'A' && value <= u'Z');
    }

    /// @brief ASCII 数字判断。
    [[nodiscard]] constexpr bool is_ascii_digit(char16_t value) noexcept
    {
        return value >= u'0' && value <= u'9';
    }

    /**
     * @brief 标识符起始字符判断。
     *
     * 目前把 U+0080 以上都近似视为字母；接入完整 Unicode 表后可替换。
     */
    [[nodiscard]] constexpr bool is_identifier_start(char16_t value) noexcept
    {
        return is_ascii_letter(value) || value == u'_' || value >= 0x80;
    }

    /// @brief 标识符后续字符判断。
    [[nodiscard]] constexpr bool is_identifier_part(char16_t value) noexcept
    {
        return is_identifier_start(value) || is_ascii_digit(value);
    }

    /// @brief 判断整个文本是否为合法标识符。
    [[nodiscard]] inline bool is_identifier(std::u16string_view text) noexcept
    {
        if (text.empty() || !is_identifier_start(text[0])) {
            return false;
        }

        for (std::size_t index = 1; index < text.size(); ++index) {
            if (!is_identifier_part(text[index])) {
                return false;
            }
        }

        return true;
    }

    /// @brief 解析 true/false；成功时写入 value。
    [[nodiscard]] inline bool try_parse_boolean(
        std::u16string_view text, bool& value) noexcept
    {
        if (text == u"true") {
            value = true;
            return true;
        }
        if (text == u"false") {
            value = false;
            return true;
        }
        return false;
    }

} // namespace weftline::syntax
