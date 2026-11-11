#include "weftline/binding/symbol_table.hpp"

#include <utility>

namespace weftline::binding {

    // 符号名在项目内全局唯一；已存在时保留首次声明，返回 false。
    bool SymbolTable::try_declare(SymbolEntry entry)
    {
        if (symbols_.find(entry.name) != symbols_.end()) {
            return false;
        }

        // 同时维护按种类的索引，供 has_variable/has_character/... 快速查询。
        switch (entry.kind) {
            case SymbolKind::Variable:
                variables_.insert(entry.name);
                break;
            case SymbolKind::Character:
                characters_.insert(entry.name);
                break;
            case SymbolKind::Item:
                items_.insert(entry.name);
                break;
            case SymbolKind::Scene:
                scenes_.insert(entry.name);
                break;
        }

        symbols_.emplace(entry.name, std::move(entry));
        return true;
    }

    const SymbolEntry* SymbolTable::find(std::u16string_view name) const
    {
        const auto found = symbols_.find(std::u16string(name));
        return found == symbols_.end() ? nullptr : &found->second;
    }

    bool SymbolTable::has_variable(std::u16string_view name) const
    {
        return variables_.find(std::u16string(name)) != variables_.end();
    }

    bool SymbolTable::has_character(std::u16string_view name) const
    {
        return characters_.find(std::u16string(name)) != characters_.end();
    }

    bool SymbolTable::has_item(std::u16string_view name) const
    {
        return items_.find(std::u16string(name)) != items_.end();
    }

    bool SymbolTable::has_scene(std::u16string_view name) const
    {
        return scenes_.find(std::u16string(name)) != scenes_.end();
    }

} // namespace weftline::binding
