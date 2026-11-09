#include "weftline/core.h"

#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <string>
#include <string_view>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/json/json_writer.hpp"
#include "weftline/source/source_text.hpp"
#include "weftline/syntax/parser.hpp"

namespace {

    [[nodiscard]] std::string parse_error_json(
        std::string_view file_path,
        std::int64_t request_version,
        std::string_view message)
    {
        weftline::json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(1);
        writer.key("version").string(weft_version());
        writer.key("filePath").string(file_path);
        writer.key("requestVersion").number(request_version);
        writer.key("hasErrors").boolean(true);
        writer.key("diagnostics").begin_array().end_array();
        writer.key("error").string(message);
        writer.end_object();
        return writer.str();
    }

    [[nodiscard]] std::string parse_to_json(
        std::string_view file_path,
        std::string_view source_utf8,
        std::int64_t request_version)
    {
        const auto source =
            weftline::source::SourceText::from_utf8(
                source_utf8, std::string(file_path));

        weftline::diagnostics::DiagnosticBag diagnostics;
        static_cast<void>(weftline::syntax::parse(source, diagnostics));

        weftline::json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(1);
        writer.key("version").string(weft_version());
        writer.key("filePath").string(file_path);
        writer.key("requestVersion").number(request_version);
        writer.key("hasErrors").boolean(diagnostics.has_errors());

        writer.key("diagnostics").begin_array();
        for (const auto& diagnostic : diagnostics) {
            writer.begin_object();
            writer.key("code").string(diagnostic.code);
            writer.key("severity").string(
                std::string(weftline::diagnostics::severity_name(diagnostic.severity)));
            writer.key("message").string(
                weftline::source::utf16_to_utf8(diagnostic.message));
            writer.key("start").number(diagnostic.span.start);
            writer.key("length").number(diagnostic.span.length);
            writer.end_object();
        }
        writer.end_array();

        writer.end_object();
        return writer.str();
    }

    [[nodiscard]] char* duplicate_string(const std::string& value)
    {
        auto* result = static_cast<char*>(std::malloc(value.size() + 1));
        if (result == nullptr) {
            return nullptr;
        }
        std::memcpy(result, value.c_str(), value.size() + 1);
        return result;
    }

} // namespace

extern "C" {

char* weft_parse_json(const char* path, const char* text, long long version)
{
    const std::string_view file_path = path != nullptr ? path : "";
    const std::string_view source = text != nullptr ? text : "";
    const auto request_version = static_cast<std::int64_t>(version);

    try {
        return duplicate_string(
            parse_to_json(file_path, source, request_version));
    } catch (const std::exception& error) {
        return duplicate_string(
            parse_error_json(file_path, request_version, error.what()));
    } catch (...) {
        return duplicate_string(
            parse_error_json(file_path, request_version, "unknown native error"));
    }
}

} // extern "C"
