#pragma once

#include <cstddef>
#include <memory>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

#include "weftline/diagnostics/diagnostic.hpp"
#include "weftline/ir/graph_analysis.hpp"
#include "weftline/ir/graph_projection.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/projects/project_index.hpp"
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
     * @brief 单个文件的模块绑定诊断缓存。
     */
    struct CachedModuleBinding
    {
        std::weak_ptr<const syntax::WeftFile> ast;
        std::vector<ProjectDiagnostic> diagnostics;
    };

    /**
     * @brief 文件级绑定缓存。
     *
     * 模块引用诊断只依赖全局符号表；符号表签名不变时，未变化文件可直接复用。
     */
    struct BindingCache
    {
        bool has_symbol_signature{};
        std::size_t symbol_signature{};
        std::unordered_map<std::string, CachedModuleBinding> files;
    };

    /**
     * @brief 图/分析/投影缓存；只含 .weft AST，不含 .wstory 描述。
     */
    struct CachedGraph
    {
        std::vector<ProjectFile> files;
        std::shared_ptr<const ir::StoryGraph> graph;
        std::shared_ptr<const ir::GraphAnalysis> analysis;
        std::shared_ptr<const ir::GraphProjection> projection;
        bool has_projection{};
    };

    /**
     * @brief 解析、绑定和图阶段缓存。
     *
     * 解析缓存按 path + text 复用 AST；绑定缓存按全局符号签名 + AST 指针复用模块诊断；
     * 图缓存按 .weft AST 集合复用它和 GraphAnalysis/GraphProjection。
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

        /// @brief 文件级绑定缓存。
        [[nodiscard]] BindingCache& binding_cache() noexcept
        {
            return binding_;
        }

        /// @brief 图/分析/投影缓存。
        [[nodiscard]] CachedGraph& graph_cache() noexcept
        {
            return graph_;
        }

        /// @brief 清空全部缓存。
        void clear();

    private:
        std::unordered_map<std::string, CachedWeft> weft_;
        std::unordered_map<std::string, CachedWStory> wstory_;
        BindingCache binding_;
        CachedGraph graph_;
    };

} // namespace weftline::projects
