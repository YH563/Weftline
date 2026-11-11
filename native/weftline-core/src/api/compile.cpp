#include "weftline/api/compile.hpp"

#include <cstddef>
#include <string>
#include <string_view>

#include "weftline/core.h"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/json/json_writer.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/lexer.hpp"

namespace weftline::api {
    namespace {

        // 最小 JSON 字符串反转义；只服务于当前占位请求解析。
        [[nodiscard]]
        std::string unescape_json(std::string_view input)
        {
            std::string result;
            result.reserve(input.size());

            for (std::size_t index = 0; index < input.size(); ++index) {
                const char value = input[index];
                if (value != '\\') {
                    result.push_back(value);
                    continue;
                }

                if (index + 1 >= input.size()) {
                    break;
                }

                const char escape = input[++index];
                switch (escape) {
                    case '"':
                        result.push_back('"');
                        break;
                    case '\\':
                        result.push_back('\\');
                        break;
                    case '/':
                        result.push_back('/');
                        break;
                    case 'b':
                        result.push_back('\b');
                        break;
                    case 'f':
                        result.push_back('\f');
                        break;
                    case 'n':
                        result.push_back('\n');
                        break;
                    case 'r':
                        result.push_back('\r');
                        break;
                    case 't':
                        result.push_back('\t');
                        break;
                    case 'u': {
                        if (index + 4 >= input.size()) {
                            break;
                        }

                        unsigned code = 0;
                        for (int digit = 0; digit < 4; ++digit) {
                            const char hex = input[index + 1 + static_cast<std::size_t>(digit)];
                            code <<= 4;
                            if (hex >= '0' && hex <= '9') {
                                code |= static_cast<unsigned>(hex - '0');
                            } else if (hex >= 'a' && hex <= 'f') {
                                code |= static_cast<unsigned>(hex - 'a' + 10);
                            } else if (hex >= 'A' && hex <= 'F') {
                                code |= static_cast<unsigned>(hex - 'A' + 10);
                            }
                        }
                        index += 4;

                        // Encodes a BMP code unit. Surrogate pairs are not combined here;
                        // the request should contain pre-encoded UTF-8 text instead.
                        if (code <= 0x7F) {
                            result.push_back(static_cast<char>(code));
                        } else if (code <= 0x7FF) {
                            result.push_back(static_cast<char>(0xC0 | (code >> 6)));
                            result.push_back(static_cast<char>(0x80 | (code & 0x3F)));
                        } else {
                            result.push_back(static_cast<char>(0xE0 | (code >> 12)));
                            result.push_back(static_cast<char>(0x80 | ((code >> 6) & 0x3F)));
                            result.push_back(static_cast<char>(0x80 | (code & 0x3F)));
                        }
                        break;
                    }
                    default:
                        result.push_back(escape);
                        break;
                }
            }

            return result;
        }

        // 在请求 JSON 中查找字符串字段；不处理嵌套对象。
        [[nodiscard]]
        bool extract_string_field(
            std::string_view json, std::string_view key, std::string& out)
        {
            const std::string quoted_key = "\"" + std::string(key) + "\"";
            auto position = json.find(quoted_key);
            if (position == std::string_view::npos) {
                return false;
            }

            position = json.find(':', position + quoted_key.size());
            if (position == std::string_view::npos) {
                return false;
            }

            position = json.find('"', position + 1);
            if (position == std::string_view::npos) {
                return false;
            }

            std::size_t end = position + 1;
            bool escaped = false;
            while (end < json.size()) {
                const char value = json[end];
                if (escaped) {
                    escaped = false;
                } else if (value == '\\') {
                    escaped = true;
                } else if (value == '"') {
                    break;
                }
                ++end;
            }

            if (end >= json.size()) {
                return false;
            }

            out = unescape_json(json.substr(position + 1, end - position - 1));
            return true;
        }

    } // namespace

    // 统一错误响应格式，保证调用方总能拿到 JSON。
    std::string error_json(std::string_view message)
    {
        json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(1);
        writer.key("version").string(weft_version());
        writer.key("error").string(message);
        writer.end_object();
        return writer.str();
    }

    std::string compile_source_to_json(std::string_view source_utf8, std::string_view file_path)
    {
        // 外部文本统一转 UTF-16，保证 start/length 与 C#/ArkTS 偏移一致。
        const auto source = source::SourceText::from_utf8(source_utf8, std::string(file_path));
        const auto lexed = syntax::Lexer(source).tokenize();

        json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(1);
        writer.key("version").string(weft_version());
        writer.key("filePath").string(file_path);
        writer.key("length").number(source.length());

        // token 文本经 UTF-16 -> UTF-8 后写入 JSON；span 保持 UTF-16 offset。
        writer.key("tokens").begin_array();
        for (const auto& token : lexed.tokens) {
            writer.begin_object();
            writer.key("kind").string(
                source::utf16_to_utf8(syntax::token_kind_name(token.kind)));
            writer.key("start").number(token.span.start);
            writer.key("length").number(token.span.length);
            writer.key("text").string(source::utf16_to_utf8(source.get_text(token.span)));
            writer.end_object();
        }
        writer.end_array();

        // 诊断只暴露稳定字段，不泄漏内部类型。
        writer.key("diagnostics").begin_array();
        for (const auto& diagnostic : lexed.diagnostics) {
            writer.begin_object();
            writer.key("code").string(diagnostic.code);
            writer.key("severity").string(std::string(diagnostics::severity_name(diagnostic.severity)));
            writer.key("message").string(source::utf16_to_utf8(diagnostic.message));
            writer.key("start").number(diagnostic.span.start);
            writer.key("length").number(diagnostic.span.length);
            writer.end_object();
        }
        writer.end_array();

        writer.end_object();
        return writer.str();
    }

    std::string compile_to_json(std::string_view request_json)
    {
        std::string text;
        std::string file_path;

        const bool has_text = extract_string_field(request_json, "text", text);
        static_cast<void>(extract_string_field(request_json, "filePath", file_path));

        // 缺少 text 时返回结构化错误，而不是空响应。
        if (!has_text) {
            return error_json("missing required field: text");
        }

        return compile_source_to_json(text, file_path);
    }

} // namespace weftline::api
