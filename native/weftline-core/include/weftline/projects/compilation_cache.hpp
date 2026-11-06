#pragma once

#include <memory>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/syntax/ast.hpp"

namespace weftline::projects {

    /**
     * @brief 一个已解析 .weft 文件的缓存项。
     */
    struct CachedWeft
    {
        std::string text;
        std::shared_ptr<const syntax::WeftFile> ast;
        std::vector<diagnostics::Diagnostic> diagnostics;
    };

    /**
     * @brief 一个已解析 .wstory 文件的缓存项。
     */
    struct CachedWStory
    {
        std::string text;
        std::shared_ptr<const syntax::WStoryFile> ast;
        std::vector<diagnostics::Diagnostic> diagnostics;
    };

    /**
     * @brief 解析阶段缓存：路径 + 文本 → AST + 解析诊断。
     *
     * 只缓存解析阶段；绑定、校验、图和投影仍然全量重建。
     * 路径作为不透明 key，不做文件系统归一化。
     */
    class CompilationCache
    {
    public:
        /// @brief 文本未变化时返回缓存的 .weft 结果；否则返回 nullptr。
        [[nodiscard]] const CachedWeft* find_weft(
            std::string_view path, std::string_view text) const;

        /// @brief 缓存一次 .weft 解析结果；ast 可以为空。
        void set_weft(
            std::string_view path,
            std::string_view text,
            std::shared_ptr<const syntax::WeftFile> ast,
            std::vector<diagnostics::Diagnostic> diagnostics);

        /// @brief 文本未变化时返回缓存的 .wstory 结果；否则返回 nullptr。
        [[nodiscard]] const CachedWStory* find_wstory(
            std::string_view path, std::string_view text) const;

        /// @brief 缓存一次 .wstory 解析结果；ast 可以为空。
        void set_wstory(
            std::string_view path,
            std::string_view text,
            std::shared_ptr<const syntax::WStoryFile> ast,
            std::vector<diagnostics::Diagnostic> diagnostics);

        /// @brief 清空全部缓存。
        void clear();

    private:
        std::unordered_map<std::string, CachedWeft> weft_;
        std::unordered_map<std::string, CachedWStory> wstory_;
    };

} // namespace weftline::projects
