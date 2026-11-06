#pragma once

#include <memory>
#include <optional>
#include <string>
#include <unordered_map>
#include <vector>

#include "weftline/binding/symbol_table.hpp"
#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/source/location.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::projects {

    /**
     * @brief 一个已解析的 .weft 文件及其路径。
     */
    struct ProjectFile
    {
        std::string file_path;
        std::shared_ptr<const syntax::WeftFile> ast;
    };

    /**
     * @brief 一个章节的定义位置。
     */
    struct ChapterDefinition
    {
        std::u16string title;
        std::string file_path;
        source::TextSpan span;
        source::TextSpan title_span;
    };

    /**
     * @brief 一个模块的定义位置。
     */
    struct ModuleDefinition
    {
        std::u16string title;
        std::u16string chapter;
        std::string file_path;
        source::TextSpan span;
        source::TextSpan title_span;
        std::optional<std::u16string> scene;
    };

    /**
     * @brief 项目级索引：合并后的符号、章节、模块和文件列表。
     */
    struct ProjectIndex
    {
        std::unordered_map<std::u16string, binding::SymbolEntry> symbols;
        std::unordered_map<std::u16string, ChapterDefinition> chapters;
        std::unordered_map<std::u16string, ModuleDefinition> modules;
        std::vector<ProjectFile> files;
    };

    /**
     * @brief 一条携带文件路径的项目诊断。
     */
    struct ProjectDiagnostic
    {
        std::string file_path;
        diagnostics::Diagnostic diagnostic;
    };

    /**
     * @brief 项目级绑定结果：索引与按文件归属的诊断。
     */
    struct ProjectBindingResult
    {
        ProjectIndex index;
        std::vector<ProjectDiagnostic> diagnostics;
    };

} // namespace weftline::projects
