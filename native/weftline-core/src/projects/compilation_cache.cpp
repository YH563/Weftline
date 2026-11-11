#include "weftline/projects/compilation_cache.hpp"

#include <utility>

namespace weftline::projects {

    // 缓存 key 是 path，但必须同时比较全文；路径相同不代表内容相同。
    const CachedWeft* CompilationCache::find_weft(
        std::string_view path, std::string_view text) const
    {
        const auto found = weft_.find(std::string(path));
        if (found == weft_.end() || std::string_view(found->second.text) != text) {
            return nullptr;
        }
        return &found->second;
    }

    // 直接覆盖同路径旧项；旧 AST 若无人引用会在替换时释放。
    void CompilationCache::set_weft(
        std::string_view path,
        std::string_view text,
        std::shared_ptr<const syntax::WeftFile> ast,
        std::vector<diagnostics::Diagnostic> diagnostics)
    {
        weft_[std::string(path)] = CachedWeft{
            std::string(text), std::move(ast), std::move(diagnostics)};
    }

    // .wstory 只影响项目信息和描述，单独缓存，避免误用 .weft 的 AST。
    const CachedWStory* CompilationCache::find_wstory(
        std::string_view path, std::string_view text) const
    {
        const auto found = wstory_.find(std::string(path));
        if (found == wstory_.end() || std::string_view(found->second.text) != text) {
            return nullptr;
        }
        return &found->second;
    }

    void CompilationCache::set_wstory(
        std::string_view path,
        std::string_view text,
        std::shared_ptr<const syntax::WStoryFile> ast,
        std::vector<diagnostics::Diagnostic> diagnostics)
    {
        wstory_[std::string(path)] = CachedWStory{
            std::string(text), std::move(ast), std::move(diagnostics)};
    }

    // 换项目或需要完全重编译时清空所有阶段，避免跨项目复用旧 key。
    void CompilationCache::clear()
    {
        weft_.clear();
        wstory_.clear();
        binding_ = BindingCache{};
        graph_ = CachedGraph{};
    }

} // namespace weftline::projects
