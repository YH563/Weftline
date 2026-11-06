#pragma once

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace weftline::json {

    /**
     * @brief 极简 JSON 输出器。
     *
     * 只负责拼字符串，不做解析；通过 begin_object/begin_array 维护层级与逗号。
     * 输出不保证缩进，适合作为跨语言边界上的紧凑响应。
     */
    class Writer
    {
    public:
        /// @brief 开始一个 JSON 对象。
        Writer& begin_object()
        {
            return open('{');
        }

        /// @brief 结束当前 JSON 对象。
        Writer& end_object()
        {
            return close('}');
        }

        /// @brief 开始一个 JSON 数组。
        Writer& begin_array()
        {
            return open('[');
        }

        /// @brief 结束当前 JSON 数组。
        Writer& end_array()
        {
            return close(']');
        }

        /// @brief 写入对象键；随后必须紧跟一个值。
        Writer& key(std::string_view text)
        {
            separator();
            write_string(text);
            out_.push_back(':');
            pending_value_ = true;
            return *this;
        }

        /// @brief 写入字符串值（自动转义）。
        Writer& string(std::string_view text)
        {
            before_value();
            write_string(text);
            return *this;
        }

        /// @brief 写入整数值。
        Writer& number(std::int64_t value)
        {
            before_value();
            out_ += std::to_string(value);
            return *this;
        }

        /// @brief 写入布尔值。
        Writer& boolean(bool value)
        {
            before_value();
            out_ += value ? "true" : "false";
            return *this;
        }

        /// @brief 写入一段已经是合法 JSON 的文本，用于嵌入子对象。
        Writer& raw(std::string_view json)
        {
            before_value();
            out_.append(json);
            return *this;
        }

        /// @brief 取得已生成的 JSON 文本。
        [[nodiscard]] const std::string& str() const noexcept
        {
            return out_;
        }

    private:
        Writer& open(char bracket)
        {
            before_value();
            out_.push_back(bracket);
            first_.push_back(true);
            return *this;
        }

        Writer& close(char bracket)
        {
            out_.push_back(bracket);
            first_.pop_back();
            return *this;
        }

        /// @brief 在写值前处理逗号；对象键后的第一个值不额外加逗号。
        void before_value()
        {
            if (pending_value_) {
                pending_value_ = false;
                return;
            }
            separator();
        }

        /// @brief 在当前容器中按需补逗号。
        void separator()
        {
            if (first_.empty()) {
                return;
            }
            if (!first_.back()) {
                out_.push_back(',');
            }
            first_.back() = false;
        }

        /// @brief 写入带转义的 JSON 字符串。
        void write_string(std::string_view text)
        {
            out_.push_back('"');
            for (char value : text) {
                switch (value) {
                    case '"':
                        out_ += "\\\"";
                        break;
                    case '\\':
                        out_ += "\\\\";
                        break;
                    case '\b':
                        out_ += "\\b";
                        break;
                    case '\f':
                        out_ += "\\f";
                        break;
                    case '\n':
                        out_ += "\\n";
                        break;
                    case '\r':
                        out_ += "\\r";
                        break;
                    case '\t':
                        out_ += "\\t";
                        break;
                    default:
                        if (static_cast<unsigned char>(value) < 0x20) {
                            static constexpr char Hex[] = "0123456789abcdef";
                            const auto byte = static_cast<unsigned char>(value);
                            out_ += "\\u00";
                            out_.push_back(Hex[(byte >> 4) & 0xF]);
                            out_.push_back(Hex[byte & 0xF]);
                        } else {
                            out_.push_back(value);
                        }
                        break;
                }
            }
            out_.push_back('"');
        }

        std::string out_;
        std::vector<bool> first_;
        bool pending_value_{false};
    };

} // namespace weftline::json
