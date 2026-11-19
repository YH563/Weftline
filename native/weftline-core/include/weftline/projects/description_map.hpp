#pragma once

#include <concepts>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

#include "weftline/syntax/ast.hpp"

namespace weftline::projects {

    /**
     * @brief “名字 → 描述”索引：把 .wstory 里作者写的描述补到 .weft 的声明上。
     *
     * 条目存在但描述为空时值是空 optional；名字不存在时没有条目。
     */
    using DescriptionMap =
        std::unordered_map<std::u16string, std::optional<std::u16string>>;

    /// @brief 读取名字对应的描述；没有条目或描述为空都返回空 optional。
    [[nodiscard]] inline std::optional<std::u16string> get_description(
        const DescriptionMap& descriptions, std::u16string_view name)
    {
        const auto found = descriptions.find(std::u16string(name));
        return found == descriptions.end() ? std::nullopt : found->second;
    }

    /// @brief .wstory 的声明类型必须带同名的 description 成员。
    template <typename Entry>
    concept DescribedEntry = requires(const Entry& entry) {
        requires std::same_as<
            decltype((entry.description)),
            const std::optional<syntax::TextBlock>&>;
    };

    /**
     * @brief 从 .wstory 声明收集“名字 → 描述”。
     *
     * “名字”成员按类型不同由调用方给出（章节用 title，其余用 name）；
     * “描述”成员统一叫 description，所以不需要额外参数。
     *
     * @param entries .wstory 里的声明列表，顺序不影响结果。
     * @param name 指向名字成员的指针，例如 &syntax::WStoryChapter::title。
     */
    template <DescribedEntry Entry>
    [[nodiscard]] DescriptionMap build_description_map(
        const std::vector<Entry>& entries,
        std::u16string Entry::*name)
    {
        DescriptionMap map;
        for (const auto& entry : entries) {
            const auto& value = entry.description;
            map.try_emplace(
                entry.*name,
                value ? std::optional<std::u16string>{value->text} : std::nullopt);
        }
        return map;
    }

} // namespace weftline::projects
