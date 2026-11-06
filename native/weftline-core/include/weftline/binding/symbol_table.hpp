#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <unordered_set>

#include "weftline/source/location.hpp"

namespace weftline::binding {

    /**
     * @brief 项目级符号种类。
     */
    enum class SymbolKind : std::uint8_t {
        Variable,
        Character,
        Item,
        Scene,
    };

    /**
     * @brief 一个项目级符号：名字、种类、定义位置；变量额外带初始值。
     */
    struct SymbolEntry
    {
        std::u16string name;
        SymbolKind kind{SymbolKind::Variable};
        source::SourceLocation location;
        std::optional<bool> value;
    };

    /**
     * @brief 项目级符号表。
     *
     * 同一名字只能声明一次；声明成功后会进入对应种类集合。
     */
    class SymbolTable
    {
    public:
        /// @brief 声明一个符号；返回 false 表示名字已存在。
        [[nodiscard]] bool try_declare(SymbolEntry entry);

        /// @brief 按名字查找符号；不存在返回 nullptr。
        [[nodiscard]] const SymbolEntry* find(std::u16string_view name) const;

        /// @brief 全部符号。
        [[nodiscard]] const std::unordered_map<std::u16string, SymbolEntry>& symbols() const noexcept
        {
            return symbols_;
        }

        [[nodiscard]] bool has_variable(std::u16string_view name) const;
        [[nodiscard]] bool has_character(std::u16string_view name) const;
        [[nodiscard]] bool has_item(std::u16string_view name) const;
        [[nodiscard]] bool has_scene(std::u16string_view name) const;

    private:
        std::unordered_map<std::u16string, SymbolEntry> symbols_;
        std::unordered_set<std::u16string> variables_;
        std::unordered_set<std::u16string> characters_;
        std::unordered_set<std::u16string> items_;
        std::unordered_set<std::u16string> scenes_;
    };

} // namespace weftline::binding
