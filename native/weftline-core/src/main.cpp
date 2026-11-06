#include <fstream>
#include <iostream>
#include <sstream>
#include <string>
#include <string_view>

#include "weftline/core.h"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/ast_printer.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    /// @brief 把源码转义成 JSON 字符串内容。
    std::string escape_json(std::string_view text)
    {
        std::string result;
        result.reserve(text.size());

        for (char value : text) {
            switch (value) {
                case '"':
                    result += "\\\"";
                    break;
                case '\\':
                    result += "\\\\";
                    break;
                case '\b':
                    result += "\\b";
                    break;
                case '\f':
                    result += "\\f";
                    break;
                case '\n':
                    result += "\\n";
                    break;
                case '\r':
                    result += "\\r";
                    break;
                case '\t':
                    result += "\\t";
                    break;
                default:
                    result.push_back(value);
                    break;
            }
        }

        return result;
    }

    /// @brief 读取文件或 stdin 的全部内容。
    std::string read_stream(std::istream& stream)
    {
        std::ostringstream buffer;
        buffer << stream.rdbuf();
        return buffer.str();
    }

    /// @brief 解析文件并打印 AST；诊断输出到 stderr。
    int run_parse(int argc, char** argv)
    {
        if (argc != 3) {
            std::cerr << "usage: weftc parse <file.weft|file.wstory>\n";
            return 1;
        }

        const std::string path = argv[2];
        std::ifstream file(path, std::ios::binary);
        if (!file) {
            std::cerr << "weftc: cannot open " << path << '\n';
            return 1;
        }

        const auto source =
            weftline::source::SourceText::from_utf8(read_stream(file), path);
        weftline::diagnostics::DiagnosticBag diagnostics;
        const auto script = weftline::syntax::parse(source, diagnostics);

        for (const auto& diagnostic : diagnostics) {
            const auto position = source.get_line_position(diagnostic.span.start);
            std::cerr << path << ':' << position.line << ':' << position.character
                      << ": "
                      << weftline::diagnostics::severity_name(diagnostic.severity)
                      << ' ' << diagnostic.code << ": "
                      << weftline::source::utf16_to_utf8(diagnostic.message)
                      << '\n';
        }

        if (!diagnostics.has_errors()) {
            std::cout << weftline::syntax::print_ast_utf8(script);
        }

        return diagnostics.has_errors() ? 1 : 0;
    }

} // namespace

int main(int argc, char** argv)
{
    if (argc >= 2 && std::string_view(argv[1]) == "parse") {
        return run_parse(argc, argv);
    }

    std::string source;
    std::string path;

    // 有参数时读文件，否则读 stdin。
    if (argc > 1) {
        path = argv[1];
        std::ifstream file(path, std::ios::binary);
        if (!file) {
            std::cerr << "weftc: cannot open " << path << '\n';
            return 1;
        }
        source = read_stream(file);
    } else {
        source = read_stream(std::cin);
    }

    // 构造 core 的 JSON 请求。
    const std::string request = "{\"schemaVersion\":1,\"filePath\":\""
        + escape_json(path) + "\",\"text\":\"" + escape_json(source) + "\"}";

    // 响应由 core 分配，打印后必须释放。
    char* response = weft_compile_json(request.c_str());
    if (response == nullptr) {
        std::cerr << "weftc: native core returned no response\n";
        return 1;
    }

    std::cout << response << '\n';
    weft_free(response);
    return 0;
}
