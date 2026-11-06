#pragma once

#include <optional>
#include <string>
#include <utility>
#include <variant>
#include <vector>

#include "weftline/source/source_text.hpp"
#include "weftline/support/box.hpp"

namespace weftline::syntax {

    /// @brief 复用源码模块的 UTF-16 范围类型。
    using source::TextSpan;

    /// @brief 复用 support::Box 作为递归节点指针。
    using support::Box;

    /**
     * @brief 正文中的纯文本片段。
     */
    struct TextLiteral
    {
        std::u16string text;
        TextSpan span;
    };

    /**
     * @brief 正文中的 {name} 引用。
     */
    struct TextReference
    {
        std::u16string name;
        TextSpan span;
    };

    /// @brief 一个正文片段：字面量或变量/角色/道具引用。
    using TextSegment = std::variant<TextLiteral, TextReference>;

    /**
     * @brief 旁白：[] 中的内容。
     */
    struct Narration
    {
        std::vector<TextSegment> segments;
        TextSpan span;
    };

    /**
     * @brief 对话：speaker:[...]。
     */
    struct Dialogue
    {
        std::u16string speaker;
        std::vector<TextSegment> segments;
        TextSpan span;
    };

    /**
     * @brief 插图指令：@fig "path"。
     */
    struct Figure
    {
        std::u16string path;
        TextSpan span;
    };

    /// @brief 模块内容块：旁白、对话或插图。
    using Content = std::variant<Narration, Dialogue, Figure>;

    /// @brief 布尔条件表达式；具体节点见 BoolLiteral / BoolRef / NotExpr / AndExpr / OrExpr。
    struct BoolExpr;

    /**
     * @brief 布尔字面量 true/false。
     */
    struct BoolLiteral
    {
        bool value{};
        TextSpan span;
    };

    /**
     * @brief 条件中的变量引用。
     */
    struct BoolRef
    {
        std::u16string name;
        TextSpan span;
    };

    /**
     * @brief 逻辑非：not (operand)。
     *
     * 递归节点持有 Box<BoolExpr>。BoolExpr 在 variant 之后才完整，
     * 因此显式声明析构函数，并在 BoolExpr 定义之后再 = default，
     * 避免 unique_ptr 在不完整类型上实例化析构函数。
     */
    struct NotExpr
    {
        Box<BoolExpr> operand;
        TextSpan span;

        /// @brief 构造非表达式。
        NotExpr(Box<BoolExpr> operand, TextSpan span);

        /// @brief 析构；定义在 BoolExpr 完整之后。
        ~NotExpr();

        NotExpr(NotExpr&&) noexcept = default;
        NotExpr& operator=(NotExpr&&) noexcept = default;
    };

    /**
     * @brief 逻辑与：(left and right)。
     */
    struct AndExpr
    {
        Box<BoolExpr> left;
        Box<BoolExpr> right;
        TextSpan span;

        /// @brief 构造与表达式。
        AndExpr(Box<BoolExpr> left, Box<BoolExpr> right, TextSpan span);

        /// @brief 析构；定义在 BoolExpr 完整之后。
        ~AndExpr();

        AndExpr(AndExpr&&) noexcept = default;
        AndExpr& operator=(AndExpr&&) noexcept = default;
    };

    /**
     * @brief 逻辑或：(left or right)。
     */
    struct OrExpr
    {
        Box<BoolExpr> left;
        Box<BoolExpr> right;
        TextSpan span;

        /// @brief 构造或表达式。
        OrExpr(Box<BoolExpr> left, Box<BoolExpr> right, TextSpan span);

        /// @brief 析构；定义在 BoolExpr 完整之后。
        ~OrExpr();

        OrExpr(OrExpr&&) noexcept = default;
        OrExpr& operator=(OrExpr&&) noexcept = default;
    };

    /// @brief BoolExpr 的实际节点联合。
    using BoolExprNode =
        std::variant<BoolLiteral, BoolRef, NotExpr, AndExpr, OrExpr>;

    /**
     * @brief 布尔条件表达式包装。
     *
     * 用 node 访问具体节点；递归节点通过 Box<BoolExpr> 持有子表达式。
     */
    struct BoolExpr
    {
        BoolExprNode node;
    };

    inline NotExpr::NotExpr(Box<BoolExpr> operand_value, TextSpan span_value)
        : operand(std::move(operand_value))
        , span(span_value)
    {
    }

    inline NotExpr::~NotExpr() = default;

    inline AndExpr::AndExpr(Box<BoolExpr> left_value, Box<BoolExpr> right_value, TextSpan span_value)
        : left(std::move(left_value))
        , right(std::move(right_value))
        , span(span_value)
    {
    }

    inline AndExpr::~AndExpr() = default;

    inline OrExpr::OrExpr(Box<BoolExpr> left_value, Box<BoolExpr> right_value, TextSpan span_value)
        : left(std::move(left_value))
        , right(std::move(right_value))
        , span(span_value)
    {
    }

    inline OrExpr::~OrExpr() = default;

    /**
     * @brief 构造布尔字面量。
     */
    inline BoolExpr make_bool_literal(bool value, TextSpan span)
    {
        return BoolExpr{BoolExprNode{BoolLiteral{value, span}}};
    }

    /**
     * @brief 构造变量引用条件。
     */
    inline BoolExpr make_bool_ref(std::u16string name, TextSpan span)
    {
        return BoolExpr{BoolExprNode{BoolRef{std::move(name), span}}};
    }

    /**
     * @brief 构造逻辑非条件。
     */
    inline BoolExpr make_not(BoolExpr operand, TextSpan span)
    {
        return BoolExpr{BoolExprNode{
            NotExpr{std::make_unique<BoolExpr>(std::move(operand)), span}}};
    }

    /**
     * @brief 构造逻辑与条件。
     */
    inline BoolExpr make_and(BoolExpr left, BoolExpr right, TextSpan span)
    {
        return BoolExpr{BoolExprNode{
            AndExpr{std::make_unique<BoolExpr>(std::move(left)),
                    std::make_unique<BoolExpr>(std::move(right)), span}}};
    }

    /**
     * @brief 构造逻辑或条件。
     */
    inline BoolExpr make_or(BoolExpr left, BoolExpr right, TextSpan span)
    {
        return BoolExpr{BoolExprNode{
            OrExpr{std::make_unique<BoolExpr>(std::move(left)),
                   std::make_unique<BoolExpr>(std::move(right)), span}}};
    }

    /**
     * @brief 分支效果：@set name true|false。
     */
    struct Effect
    {
        std::u16string name;
        bool value{};
        TextSpan span;
    };

    /**
     * @brief 出口块中的一条分支。
     *
     * span 覆盖整条分支；target_span / condition_span / label_span /
     * effect_span 分别指向目标、条件、文案和效果块，用于诊断与文本编辑。
     */
    struct ExitBranch
    {
        std::optional<BoolExpr> condition;
        std::optional<std::u16string> label;
        std::u16string target;
        std::vector<Effect> effects;

        TextSpan span;
        TextSpan target_span;
        std::optional<TextSpan> condition_span;
        std::optional<TextSpan> label_span;
        std::optional<TextSpan> effect_span;
    };

    /**
     * @brief 一个模块的出口块。
     *
     * character 存在表示选择模式，为空表示自动模式；branches 的顺序是语义的一部分。
     */
    struct Exit
    {
        std::optional<std::u16string> character;
        std::vector<ExitBranch> branches;
        TextSpan span;

        /// @brief 是否为选择模式（带角色）。
        [[nodiscard]]
        bool is_choice_mode() const noexcept
        {
            return character.has_value();
        }
    };

    /**
     * @brief 变量声明：@var name true|false。
     */
    struct WeftVariable
    {
        std::u16string name;
        bool value{};
        TextSpan span;
    };

    /**
     * @brief 角色声明：@char name。
     */
    struct WeftCharacter
    {
        std::u16string name;
        TextSpan span;
    };

    /**
     * @brief 道具声明：@item name。
     */
    struct WeftItem
    {
        std::u16string name;
        TextSpan span;
    };

    /**
     * @brief 场景声明：@scene name。
     */
    struct WeftScene
    {
        std::u16string name;
        TextSpan span;
    };

    /**
     * @brief 一个模块；title 是模块 ID，也是剧情图的唯一节点。
     *
     * exit 可能为空：存在语法错误时，解析器仍返回可用的部分 AST，
     * 让编辑器可以继续工作。
     */
    struct Module
    {
        std::u16string title;
        TextSpan title_span;

        std::optional<std::u16string> scene;
        std::optional<TextSpan> scene_span;

        std::vector<Content> content;
        std::optional<Exit> exit;
        TextSpan span;
    };

    /**
     * @brief 一个 .weft 模块脚本文件。
     *
     * 第一行是章节标题，之后是分组声明和若干模块；声明是项目符号的真实来源。
     */
    struct WeftFile
    {
        std::u16string title;
        TextSpan title_span;

        std::vector<WeftVariable> variables;
        std::vector<WeftCharacter> characters;
        std::vector<WeftItem> items;
        std::vector<WeftScene> scenes;
        std::vector<Module> modules;

        TextSpan span;
    };

    /**
     * @brief 纯文本块，用于描述、概要和设定文档。
     */
    struct TextBlock
    {
        std::u16string text;
        TextSpan span;
    };

    /**
     * @brief 章节描述：@chapter title [description]。
     */
    struct WStoryChapter
    {
        std::u16string title;
        TextSpan title_span;
        std::optional<TextBlock> description;
        TextSpan span;
    };

    /**
     * @brief 一条 @doc 世界设定文档。
     *
     * 适合放世界规则、时间线、阵营和术语；不参与图结构或播放执行。
     */
    struct Doc
    {
        std::u16string title;
        TextSpan title_span;
        TextBlock body;
        TextSpan span;
    };

    /**
     * @brief .wstory 中的变量声明（带可选描述）。
     */
    struct WStoryVariable
    {
        std::u16string name;
        bool value{};
        std::optional<TextBlock> description;
        TextSpan span;
    };

    /**
     * @brief .wstory 中的角色声明（带可选描述）。
     */
    struct WStoryCharacter
    {
        std::u16string name;
        std::optional<TextBlock> description;
        TextSpan span;
    };

    /**
     * @brief .wstory 中的道具声明（带可选描述）。
     */
    struct WStoryItem
    {
        std::u16string name;
        std::optional<TextBlock> description;
        TextSpan span;
    };

    /**
     * @brief .wstory 中的场景声明（带可选描述）。
     */
    struct WStoryScene
    {
        std::u16string name;
        std::optional<TextBlock> description;
        TextSpan span;
    };

    /**
     * @brief 一个 .wstory 项目总览文件。
     *
     * 声明由 .weft 汇总生成；项目信息、概要和 @doc 由作者手写。
     */
    struct WStoryFile
    {
        std::u16string project_name;
        TextSpan project_name_span;

        std::optional<TextBlock> summary;
        std::vector<WStoryChapter> chapters;
        std::vector<Doc> docs;
        std::vector<WStoryCharacter> characters;
        std::vector<WStoryItem> items;
        std::vector<WStoryScene> scenes;
        std::vector<WStoryVariable> variables;

        TextSpan span;
    };

    /// @brief 脚本文件联合：.weft 模块脚本或 .wstory 项目总览。
    using ScriptFile = std::variant<WeftFile, WStoryFile>;

    /**
     * @brief 返回条件表达式对应节点的源码范围。
     */
    [[nodiscard]]
    inline TextSpan span_of(const BoolExpr& expression)
    {
        return std::visit([](const auto& node) { return node.span; }, expression.node);
    }

    /**
     * @brief 返回脚本文件的整体源码范围。
     */
    [[nodiscard]]
    inline TextSpan span_of(const ScriptFile& file)
    {
        return std::visit([](const auto& node) { return node.span; }, file);
    }

} // namespace weftline::syntax
