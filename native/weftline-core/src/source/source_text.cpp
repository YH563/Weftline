#include "weftline/source/source_text.hpp"

#include <algorithm>
#include <cstddef>

namespace weftline::source {
    namespace {

        /// @brief UTF-16 高代理项判断。
        [[nodiscard]]
        bool is_high_surrogate(char16_t unit) noexcept
        {
            return unit >= 0xD800 && unit <= 0xDBFF;
        }

        /// @brief UTF-16 低代理项判断。
        [[nodiscard]]
        bool is_low_surrogate(char16_t unit) noexcept
        {
            return unit >= 0xDC00 && unit <= 0xDFFF;
        }

    } // namespace

    std::u16string utf8_to_utf16(std::string_view text)
    {
        // 按首字节判断后续字节数；非法序列替换为 U+FFFD。
        std::u16string result;
        result.reserve(text.size());

        std::size_t index = 0;
        while (index < text.size()) {
            const auto lead = static_cast<unsigned char>(text[index]);
            char32_t code_point = 0;
            std::size_t continuation = 0;

            if (lead < 0x80) {
                code_point = lead;
            } else if ((lead & 0xE0) == 0xC0) {
                code_point = lead & 0x1F;
                continuation = 1;
            } else if ((lead & 0xF0) == 0xE0) {
                code_point = lead & 0x0F;
                continuation = 2;
            } else if ((lead & 0xF8) == 0xF0) {
                code_point = lead & 0x07;
                continuation = 3;
            } else {
                result.push_back(u'\uFFFD');
                ++index;
                continue;
            }

            if (index + continuation >= text.size()) {
                result.push_back(u'\uFFFD');
                break;
            }

            bool valid = true;
            for (std::size_t offset = 1; offset <= continuation; ++offset) {
                const auto next = static_cast<unsigned char>(text[index + offset]);
                if ((next & 0xC0) != 0x80) {
                    valid = false;
                    break;
                }
                code_point = (code_point << 6) | (next & 0x3F);
            }

            if (!valid) {
                result.push_back(u'\uFFFD');
                ++index;
                continue;
            }

            index += continuation + 1;
            if (code_point <= 0xFFFF) {
                result.push_back(static_cast<char16_t>(code_point));
        } else {
            // 补充平面码点拆成 UTF-16 代理对。
            code_point -= 0x10000;
                result.push_back(static_cast<char16_t>(0xD800 + (code_point >> 10)));
                result.push_back(static_cast<char16_t>(0xDC00 + (code_point & 0x3FF)));
            }
        }

        return result;
    }

    std::string utf16_to_utf8(std::u16string_view text)
    {
        // 代理对合并为一个码点；孤立代理项按 U+FFFD 处理。
        std::string result;
        result.reserve(text.size());

        for (std::size_t index = 0; index < text.size(); ++index) {
            char32_t code_point = text[index];

            if (is_high_surrogate(text[index]) && index + 1 < text.size()
                && is_low_surrogate(text[index + 1])) {
                code_point = 0x10000
                    + ((static_cast<char32_t>(text[index]) - 0xD800) << 10)
                    + (static_cast<char32_t>(text[index + 1]) - 0xDC00);
                ++index;
            } else if (is_high_surrogate(text[index]) || is_low_surrogate(text[index])) {
                code_point = 0xFFFD;
            }

            if (code_point <= 0x7F) {
                result.push_back(static_cast<char>(code_point));
            } else if (code_point <= 0x7FF) {
                result.push_back(static_cast<char>(0xC0 | (code_point >> 6)));
                result.push_back(static_cast<char>(0x80 | (code_point & 0x3F)));
            } else if (code_point <= 0xFFFF) {
                result.push_back(static_cast<char>(0xE0 | (code_point >> 12)));
                result.push_back(static_cast<char>(0x80 | ((code_point >> 6) & 0x3F)));
                result.push_back(static_cast<char>(0x80 | (code_point & 0x3F)));
            } else {
                result.push_back(static_cast<char>(0xF0 | (code_point >> 18)));
                result.push_back(static_cast<char>(0x80 | ((code_point >> 12) & 0x3F)));
                result.push_back(static_cast<char>(0x80 | ((code_point >> 6) & 0x3F)));
                result.push_back(static_cast<char>(0x80 | (code_point & 0x3F)));
            }
        }

        return result;
    }

    SourceText SourceText::from_utf8(std::string_view text, std::string file_path)
    {
        // 外部输入统一转 UTF-16，保证所有 span 使用同一套偏移语义。
        return SourceText(utf8_to_utf16(text), std::move(file_path));
    }

    SourceText SourceText::from_utf16(std::u16string text, std::string file_path)
    {
        return SourceText(std::move(text), std::move(file_path));
    }

    SourceText::SourceText(std::u16string text, std::string file_path)
        : text_(std::move(text))
        , file_path_(std::move(file_path))
    {
        split_lines();
    }

    std::u16string SourceText::get_text(TextSpan span) const
    {
        // 越界返回空串，避免把非法 span 传给调用方。
        if (span.start < 0 || span.length < 0
            || static_cast<std::size_t>(span.end()) > text_.size()) {
            return {};
        }

        return text_.substr(static_cast<std::size_t>(span.start),
                            static_cast<std::size_t>(span.length));
    }

    LinePosition SourceText::get_line_position(std::int32_t position) const
    {
        if (lines_.empty()) {
            return {};
        }

        position = std::clamp(position, std::int32_t{0}, length());

        // 二分查找最后一个 start <= position 的行。
        const auto found = std::ranges::upper_bound(
            lines_, position, std::less<>{},
            [](const SourceLine& line) { return line.span.start; });

        const auto index = found == lines_.begin()
            ? std::size_t{0}
            : static_cast<std::size_t>(found - lines_.begin() - 1);

        const auto& line = lines_[index];
        const auto character = std::max<std::int32_t>(1, position - line.span.start + 1);
        return {line.number, character};
    }

    void SourceText::split_lines()
    {
        // 支持 \n、\r\n、\r；始终保留最后一行，让 EOF 有稳定行号。
        lines_.clear();

        std::int32_t line_start = 0;
        std::int32_t line_number = 1;

        for (std::int32_t index = 0; index < length(); ++index) {
            if (text_[static_cast<std::size_t>(index)] != u'\r'
                && text_[static_cast<std::size_t>(index)] != u'\n') {
                continue;
            }

            const std::int32_t line_end = index;
            std::int32_t break_length = 1;

            if (text_[static_cast<std::size_t>(index)] == u'\r' && index + 1 < length()
                && text_[static_cast<std::size_t>(index + 1)] == u'\n') {
                break_length = 2;
                ++index;
            }

            const std::int32_t line_length = line_end - line_start;
            lines_.push_back(
                {.span = {line_start, line_length},
                 .span_including_line_break = {line_start, line_length + break_length},
                 .number = line_number++});

            line_start = line_end + break_length;
        }

        // 保留最后一行（即使文件以换行结尾，也会多出一个空行）。
        const std::int32_t final_length = length() - line_start;
        lines_.push_back(
            {.span = {line_start, final_length},
             .span_including_line_break = {line_start, final_length},
             .number = line_number});
    }

} // namespace weftline::source
