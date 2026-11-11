#include "weftline/projects/compilation_cache.hpp"

#include <utility>

namespace weftline::projects {

    const CachedWeft* CompilationCache::find_weft(
        std::string_view path, std::string_view text) const
    {
        const auto found = weft_.find(std::string(path));
        if (found == weft_.end() || std::string_view(found->second.text) != text) {
            return nullptr;
        }
        return &found->second;
    }

    void CompilationCache::set_weft(
        std::string_view path,
        std::string_view text,
        std::shared_ptr<const syntax::WeftFile> ast,
        std::vector<diagnostics::Diagnostic> diagnostics)
    {
        weft_[std::string(path)] = CachedWeft{
            std::string(text), std::move(ast), std::move(diagnostics)};
    }

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

    void CompilationCache::clear()
    {
        weft_.clear();
        wstory_.clear();
        binding_ = BindingCache{};
        graph_ = CachedGraph{};
    }

} // namespace weftline::projects
