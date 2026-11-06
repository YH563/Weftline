#pragma once

#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "weftline/source/location.hpp"

namespace weftline::ir {

    /**
     * @brief 出口模式。
     */
    enum class ExitMode : std::uint8_t {
        Auto,
        Choice,
    };

    /**
     * @brief 模块内容种类。
     */
    enum class ContentKind : std::uint8_t {
        Narration,
        Dialogue,
        Figure,
    };

    /**
     * @brief 正文片段种类。
     */
    enum class TextSegmentKind : std::uint8_t {
        Text,
        Ref,
    };

    /**
     * @brief 布尔表达式种类。
     */
    enum class BoolExprKind : std::uint8_t {
        Literal,
        Ref,
        Not,
        And,
        Or,
    };

    /// @brief 返回出口模式的稳定名称。
    [[nodiscard]] constexpr const char* exit_mode_name(ExitMode mode) noexcept
    {
        return mode == ExitMode::Choice ? "choice" : "auto";
    }

    /// @brief 返回内容种类的稳定名称。
    [[nodiscard]] constexpr const char* content_kind_name(ContentKind kind) noexcept
    {
        switch (kind) {
            case ContentKind::Narration:
                return "narration";
            case ContentKind::Dialogue:
                return "dialogue";
            case ContentKind::Figure:
                return "figure";
        }
        return "unknown";
    }

    /// @brief 返回正文片段种类的稳定名称。
    [[nodiscard]] constexpr const char* text_segment_kind_name(TextSegmentKind kind) noexcept
    {
        return kind == TextSegmentKind::Ref ? "ref" : "text";
    }

    /// @brief 返回布尔表达式种类的稳定名称。
    [[nodiscard]] constexpr const char* bool_expr_kind_name(BoolExprKind kind) noexcept
    {
        switch (kind) {
            case BoolExprKind::Literal:
                return "literal";
            case BoolExprKind::Ref:
                return "ref";
            case BoolExprKind::Not:
                return "not";
            case BoolExprKind::And:
                return "and";
            case BoolExprKind::Or:
                return "or";
        }
        return "unknown";
    }

    /**
     * @brief 扁平布尔表达式；递归子节点用 shared_ptr 共享。
     *
     * 这是 IR/传输模型，保留 Kind + 可选字段，方便 JSON 与 HTML 播放器。
     */
    struct IrBoolExpr
    {
        BoolExprKind kind{BoolExprKind::Literal};
        bool value{};
        std::u16string name;
        std::shared_ptr<const IrBoolExpr> operand;
        std::shared_ptr<const IrBoolExpr> left;
        std::shared_ptr<const IrBoolExpr> right;

        /// @brief 创建布尔字面量。
        static IrBoolExpr literal(bool value);

        /// @brief 创建变量引用。
        static IrBoolExpr ref(std::u16string name);

        /// @brief 创建逻辑非。
        static IrBoolExpr not_(IrBoolExpr operand);

        /// @brief 创建逻辑与。
        static IrBoolExpr and_(IrBoolExpr left, IrBoolExpr right);

        /// @brief 创建逻辑或。
        static IrBoolExpr or_(IrBoolExpr left, IrBoolExpr right);
    };

    /**
     * @brief 正文片段：纯文本或 {name} 引用。
     */
    struct IrTextSegment
    {
        TextSegmentKind kind{TextSegmentKind::Text};
        std::u16string text;
        std::u16string name;

        /// @brief 创建纯文本片段。
        static IrTextSegment literal(std::u16string text);

        /// @brief 创建 {name} 引用片段。
        static IrTextSegment reference(std::u16string name);
    };

    /**
     * @brief 模块内容块；不复制完整正文之外的内容。
     */
    struct IrContentBlock
    {
        ContentKind kind{ContentKind::Narration};
        std::vector<IrTextSegment> segments;
        std::optional<std::u16string> speaker;
        std::optional<std::u16string> path;

        /// @brief 创建旁白块。
        static IrContentBlock narration(std::vector<IrTextSegment> segments);

        /// @brief 创建对话块。
        static IrContentBlock dialogue(
            std::u16string speaker, std::vector<IrTextSegment> segments);

        /// @brief 创建图片块。
        static IrContentBlock figure(std::u16string path);
    };

    /**
     * @brief 分支效果。
     */
    struct IrAssignment
    {
        std::u16string name;
        bool value{};
    };

    /**
     * @brief 一条出口分支。
     */
    struct IrExitBranch
    {
        std::optional<IrBoolExpr> condition;
        std::optional<std::u16string> label;
        std::u16string target;
        std::vector<IrAssignment> effects;
        source::SourceLocation source;
    };

    /**
     * @brief 一个模块的出口块。
     */
    struct IrExitBlock
    {
        ExitMode mode{ExitMode::Auto};
        std::optional<std::u16string> character;
        std::vector<IrExitBranch> branches;
        source::SourceLocation source;
    };

    /**
     * @brief 一个模块；Id 是剧情图的唯一节点。
     */
    struct IrModule
    {
        std::u16string id;
        std::u16string chapter_id;
        std::optional<std::u16string> scene;
        std::vector<IrContentBlock> content;
        std::optional<IrExitBlock> exit;
        source::SourceLocation source;
    };

    /**
     * @brief 一个全局变量的初始值。
     */
    struct IrGlobal
    {
        std::u16string name;
        bool initial{};
    };

    /**
     * @brief 剧情执行语义：全局变量、模块和出口分支。
     */
    struct StoryGraph
    {
        int schema_version{2};
        std::u16string end{u"END"};
        std::optional<std::u16string> entry;
        std::vector<IrGlobal> globals;
        std::vector<IrModule> modules;
    };

} // namespace weftline::ir
