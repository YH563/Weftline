#pragma once

#include <compare>
#include <cstddef>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace weftline::source {

    /**
     * @brief 源码中的连续区间，偏移以 UTF-16 code unit 计量。
     *
     * start 为起始偏移，length 为长度，end() 为开区间结束位置。
     * 使用 UTF-16 是为了与 C# string 和 ArkTS/JS string 的索引语义一致。
     */
    struct TextSpan
    {
        std::int32_t start{0};
        std::int32_t length{0};

        /// @brief 开区间结束位置（不含）。
        [[nodiscard]]
        constexpr std::int32_t end() const noexcept
        {
            return start + length;
        }

        /// @brief 用起止位置构造区间。
        [[nodiscard]]
        static constexpr TextSpan from_bounds(std::int32_t start, std::int32_t end) noexcept
        {
            return {start, end - start};
        }

        /// @brief 在指定位置构造空区间。
        [[nodiscard]]
        static constexpr TextSpan empty(std::int32_t position) noexcept
        {
            return {position, 0};
        }

        /// @brief 是否为零长度区间。
        [[nodiscard]]
        constexpr bool is_empty() const noexcept
        {
            return length == 0;
        }

        auto operator<=>(const TextSpan&) const = default;
    };

    /**
     * @brief 1 基的行/列位置。
     */
    struct LinePosition
    {
        std::int32_t line{1};
        std::int32_t character{1};

        auto operator<=>(const LinePosition&) const = default;
    };

    /**
     * @brief 一行源码的范围信息。
     */
    struct SourceLine
    {
        /// 不含换行符的文本范围。
        TextSpan span;
        /// 含换行符的文本范围。
        TextSpan span_including_line_break;
        /// 1 基行号。
        std::int32_t number{1};
    };

    /**
     * @brief 不可变的源码文本，负责行拆分与偏移/位置换算。
     *
     * 内部始终保存 UTF-16 文本；UTF-8 只在边界处转换。
     */
    class SourceText
    {
    public:
        /**
         * @brief 从 UTF-8 文本创建；会自动转换为 UTF-16。
         */
        static SourceText from_utf8(std::string_view text, std::string file_path = {});

        /**
         * @brief 直接使用已有的 UTF-16 文本创建。
         */
        static SourceText from_utf16(std::u16string text, std::string file_path = {});

        /// @brief 完整的 UTF-16 源码文本。
        [[nodiscard]]
        const std::u16string& text() const noexcept
        {
            return text_;
        }

        /// @brief 源文件路径；内存文本时为空。
        [[nodiscard]]
        const std::string& file_path() const noexcept
        {
            return file_path_;
        }

        /// @brief 源码长度（UTF-16 code unit）。
        [[nodiscard]]
        std::int32_t length() const noexcept
        {
            return static_cast<std::int32_t>(text_.size());
        }

        /// @brief 拆分后的行列表。
        [[nodiscard]]
        const std::vector<SourceLine>& lines() const noexcept
        {
            return lines_;
        }

        /**
         * @brief 返回一行源码的文本视图（不含换行符）。
         *
         * 视图指向 SourceText 内部缓冲；SourceText 存活期间有效。
         */
        [[nodiscard]]
        std::u16string_view line_text(const SourceLine& line) const noexcept
        {
            const auto start = static_cast<std::size_t>(line.span.start);
            const auto length = static_cast<std::size_t>(line.span.length);
            if (start > text_.size() || length > text_.size() - start) {
                return {};
            }
            return std::u16string_view(text_).substr(start, length);
        }

        /**
         * @brief 返回区间对应的 UTF-16 文本；越界时返回空串。
         */
        [[nodiscard]]
        std::u16string get_text(TextSpan span) const;

        /**
         * @brief 把 UTF-16 偏移换算成 1 基行列位置。
         */
        [[nodiscard]]
        LinePosition get_line_position(std::int32_t position) const;

    private:
        SourceText(std::u16string text, std::string file_path);

        /// @brief 按 \n、\r\n、\r 拆行；始终保留最后一行。
        void split_lines();

        std::u16string text_;
        std::string file_path_;
        std::vector<SourceLine> lines_;
    };

    /**
     * @brief UTF-8 -> UTF-16；非法序列替换为 U+FFFD。
     */
    [[nodiscard]]
    std::u16string utf8_to_utf16(std::string_view text);

    /**
     * @brief UTF-16 -> UTF-8；非法代理项替换为 U+FFFD。
     */
    [[nodiscard]]
    std::string utf16_to_utf8(std::u16string_view text);

} // namespace weftline::source
