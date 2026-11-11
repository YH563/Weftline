#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <type_traits>
#include <vector>

#include "weftline/ir/graph_analysis.hpp"
#include "weftline/ir/story_graph.hpp"
#include "weftline/source/location.hpp"

namespace weftline::ir {

    /**
     * @brief 章节在投影中的状态。
     */
    enum class GraphChapterFlags : std::uint8_t {
        None = 0,
        Entry = 1,
        HasIncoming = 2,
        HasOutgoing = 4,
        HasEndEdge = 8,
    };

    /**
     * @brief 模块在投影中的状态。
     */
    enum class GraphModuleFlags : std::uint8_t {
        None = 0,
        Entry = 1,
        TargetsEnd = 2,
        Unreachable = 4,
        CannotReachEnd = 8,
    };

    [[nodiscard]] constexpr GraphChapterFlags operator|(
        GraphChapterFlags left, GraphChapterFlags right) noexcept
    {
        return static_cast<GraphChapterFlags>(
            static_cast<std::uint8_t>(left) | static_cast<std::uint8_t>(right));
    }

    [[nodiscard]] constexpr GraphModuleFlags operator|(
        GraphModuleFlags left, GraphModuleFlags right) noexcept
    {
        return static_cast<GraphModuleFlags>(
            static_cast<std::uint8_t>(left) | static_cast<std::uint8_t>(right));
    }

    /// @brief 判断某个 flag 是否置位。
    template <typename Flags>
    [[nodiscard]] constexpr bool has_flag(Flags value, Flags flag) noexcept
    {
        using Underlying = std::underlying_type_t<Flags>;
        return (static_cast<Underlying>(value) & static_cast<Underlying>(flag)) != 0;
    }

    /**
     * @brief 章节容器：标题、有序模块 ID 和状态标记。
     */
    struct GraphChapter
    {
        std::u16string chapter_id;
        std::u16string title;
        std::vector<std::u16string> module_ids;
        GraphChapterFlags flags{GraphChapterFlags::None};
    };

    /**
     * @brief 模块摘要：正文首行、对话数和选项数。
     */
    struct GraphModuleSummary
    {
        std::optional<std::u16string> first_line;
        std::int32_t dialogue_count{};
        std::int32_t choice_count{};
    };

    /**
     * @brief 模块节点：图视图需要的元数据，不含完整正文。
     */
    struct GraphModuleNode
    {
        std::u16string module_id;
        std::u16string chapter_id;
        std::optional<std::u16string> scene;
        std::int32_t branch_count{};
        GraphModuleFlags flags{GraphModuleFlags::None};
        GraphModuleSummary summary;
        source::SourceLocation source;
    };

    /**
     * @brief 一条真实模块边；external 表示目标位于其他章节。
     */
    struct GraphModuleEdge
    {
        std::u16string from_module;
        std::u16string to_module;
        ExitMode mode{ExitMode::Auto};
        std::optional<std::u16string> label;
        std::optional<IrBoolExpr> condition;
        bool external{};
        source::SourceLocation source;
    };

    /**
     * @brief 剧情图的压缩投影。
     *
     * 不复制完整正文；自带图视图需要的模块摘要、章节边和引用索引。
     */
    struct GraphProjection
    {
        std::optional<std::u16string> entry_module_id;
        std::vector<GraphChapter> chapters;
        std::vector<GraphModuleNode> modules;
        std::vector<GraphModuleEdge> module_edges;
        std::vector<ChapterEdge> chapter_edges;
        ReferenceIndex references;
    };

} // namespace weftline::ir
