#pragma once

#include <span>
#include <string>
#include <string_view>

#include "weftline/syntax/ast.hpp"

namespace weftline::generation {

    /**
     * @brief 从 .weft 声明生成 .wstory 文本。
     *
     * 声明从 weft_files 反射；项目名、概要和 @doc 来自 existing；
     * 描述按名字保留，新条目得到空描述。
     *
     * @param weft_files 已解析的 .weft 文件，顺序决定生成顺序。
     * @param existing 现有 .wstory；可为空。
     * @param fallback_project_name 现有项目名为空时使用。
     * @return UTF-16 的 .wstory 文本。
     *
     * @note 现有 `@entry` 会原样保留；缺失时不自动写入。
     */
    [[nodiscard]] std::u16string generate(
        std::span<const syntax::WeftFile> weft_files,
        const syntax::WStoryFile* existing,
        std::u16string_view fallback_project_name);

    /**
     * @brief 从已加载项目的 .weft AST 生成 .wstory 文本。
     *
     * 用于 weft_project_compile：项目文件以 shared_ptr 持有 AST，不能直接
     * 复用连续对象版本；两种入口的输出保持一致。
     *
     * @param weft_files 已解析的 .weft AST，顺序决定生成顺序。
     * @param existing 现有 .wstory；可为空。
     * @param fallback_project_name 现有项目名为空时使用。
     * @return UTF-16 的 .wstory 文本。
     *
     * @note 现有 `@entry` 会原样保留；缺失时不自动写入。
     */
    [[nodiscard]] std::u16string generate(
        std::span<const syntax::WeftFile*> weft_files,
        const syntax::WStoryFile* existing,
        std::u16string_view fallback_project_name);

} // namespace weftline::generation
