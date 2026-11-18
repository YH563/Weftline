#include "weftline/tools/lexer_json.hpp"

#include "weftline/core.h"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/json/json_writer.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/lexer.hpp"

namespace weftline::tools {

    std::string lexer_json(std::string_view source_utf8, std::string_view file_path)
    {
        // 外部文本统一转 UTF-16，保证 start/length 与 C#/ArkTS 偏移一致。
        const auto source =
            source::SourceText::from_utf8(source_utf8, std::string(file_path));
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
            writer.key("severity").string(
                std::string(diagnostics::severity_name(diagnostic.severity)));
            writer.key("message").string(
                source::utf16_to_utf8(diagnostic.message));
            writer.key("start").number(diagnostic.span.start);
            writer.key("length").number(diagnostic.span.length);
            writer.end_object();
        }
        writer.end_array();

        writer.end_object();
        return writer.str();
    }

} // namespace weftline::tools
