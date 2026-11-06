#pragma once

#include <cstddef>
#include <optional>
#include <string_view>
#include <vector>

namespace weftline::source {

    /// @brief 是否为解析器认可的空白字符。
    [[nodiscard]] constexpr bool is_space(char16_t value) noexcept
    {
        return value == u' ' || value == u'\t' || value == u'\r' || value == u'\n'
            || value == u'\f' || value == u'\v';
    }

    /// @brief 去掉开头的空白。
    [[nodiscard]] inline std::u16string_view trim_start(std::u16string_view text) noexcept
    {
        std::size_t index = 0;
        while (index < text.size() && is_space(text[index])) {
            ++index;
        }
        return text.substr(index);
    }

    /// @brief 去掉末尾的空白。
    [[nodiscard]] inline std::u16string_view trim_end(std::u16string_view text) noexcept
    {
        std::size_t size = text.size();
        while (size > 0 && is_space(text[size - 1])) {
            --size;
        }
        return text.substr(0, size);
    }

    /// @brief 去掉首尾空白。
    [[nodiscard]] inline std::u16string_view trim(std::u16string_view text) noexcept
    {
        return trim_end(trim_start(text));
    }

    /// @brief 是否以指定前缀开头。
    [[nodiscard]] inline bool starts_with(
        std::u16string_view text, std::u16string_view prefix) noexcept
    {
        return text.size() >= prefix.size()
            && text.compare(0, prefix.size(), prefix) == 0;
    }

    /// @brief 指定位置是否以给定前缀开头。
    [[nodiscard]] inline bool starts_with_at(
        std::u16string_view text, std::size_t position, std::u16string_view prefix) noexcept
    {
        return position <= text.size()
            && text.size() - position >= prefix.size()
            && text.compare(position, prefix.size(), prefix) == 0;
    }

    /// @brief 查找字符；未找到返回 std::u16string_view::npos。
    [[nodiscard]] inline std::size_t index_of(
        std::u16string_view text, char16_t value, std::size_t position = 0) noexcept
    {
        return text.find(value, position);
    }

    /// @brief 查找子串；未找到返回 std::u16string_view::npos。
    [[nodiscard]] inline std::size_t index_of(
        std::u16string_view text, std::u16string_view needle, std::size_t position = 0) noexcept
    {
        return text.find(needle, position);
    }

    /// @brief 从 open_index 处的 '(' 找到匹配的 ')'。
    [[nodiscard]] inline std::optional<std::size_t> find_matching_paren(
        std::u16string_view text, std::size_t open_index) noexcept
    {
        if (open_index >= text.size() || text[open_index] != u'(') {
            return std::nullopt;
        }

        std::int32_t depth = 0;
        for (std::size_t index = open_index; index < text.size(); ++index) {
            if (text[index] == u'(') {
                ++depth;
            } else if (text[index] == u')') {
                --depth;
                if (depth == 0) {
                    return index;
                }
            }
        }

        return std::nullopt;
    }

    /// @brief 按空白拆分，返回指向原文本的视图。
    [[nodiscard]] inline std::vector<std::u16string_view> split_whitespace(
        std::u16string_view text)
    {
        std::vector<std::u16string_view> parts;
        std::size_t position = 0;

        while (position < text.size()) {
            while (position < text.size() && is_space(text[position])) {
                ++position;
            }
            if (position >= text.size()) {
                break;
            }

            const auto start = position;
            while (position < text.size() && !is_space(text[position])) {
                ++position;
            }
            parts.push_back(text.substr(start, position - start));
        }

        return parts;
    }

} // namespace weftline::source
