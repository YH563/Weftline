#include "weftline/syntax/condition_parser.hpp"

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>
#include <utility>

#include "weftline/source/text_utils.hpp"
#include "weftline/syntax/syntax_facts.hpp"

namespace weftline::syntax {
    namespace {

        // 递归下降解析布尔条件：or > and > not > primary。
        // 输入是模块文本的局部片段，offset 用于把局部位置换算回文件绝对位置。
        class ConditionParser
        {
        public:
            ConditionParser(
                std::u16string_view text,
                std::int32_t offset,
                diagnostics::DiagnosticBag& diagnostics) noexcept
                : text_(text)
                , offset_(offset)
                , diagnostics_(diagnostics)
            {
            }

            [[nodiscard]] std::optional<BoolExpr> parse_all()
            {
                auto expression = parse_or();

                skip_whitespace();
                if (position_ < text_.size()) {
                    report(u"条件表达式后存在多余内容。", position_, 1);
                }

                return expression;
            }

        private:
            // 优先级最低：a or b or c。
            [[nodiscard]] std::optional<BoolExpr> parse_or()
            {
                auto left = parse_and();

                while (left) {
                    skip_whitespace();
                    const auto keyword_start = position_;

                    if (!try_consume_keyword(u"or")) {
                        break;
                    }

                    auto right = parse_and();
                    if (!right) {
                        report(u"or 后缺少操作数。", keyword_start, 2);
                        return left;
                    }

                    const auto left_span = span_of(*left);
                    const auto right_span = span_of(*right);
                    left = make_or(
                        std::move(*left),
                        std::move(*right),
                        source::TextSpan::from_bounds(left_span.start, right_span.end()));
                }

                return left;
            }

            // 优先级高于 or：a and b and c。
            [[nodiscard]] std::optional<BoolExpr> parse_and()
            {
                auto left = parse_unary();

                while (left) {
                    skip_whitespace();
                    const auto keyword_start = position_;

                    if (!try_consume_keyword(u"and")) {
                        break;
                    }

                    auto right = parse_unary();
                    if (!right) {
                        report(u"and 后缺少操作数。", keyword_start, 3);
                        return left;
                    }

                    const auto left_span = span_of(*left);
                    const auto right_span = span_of(*right);
                    left = make_and(
                        std::move(*left),
                        std::move(*right),
                        source::TextSpan::from_bounds(left_span.start, right_span.end()));
                }

                return left;
            }

            // not 右结合，可以连续写 not not x。
            [[nodiscard]] std::optional<BoolExpr> parse_unary()
            {
                skip_whitespace();
                const auto start = position_;

                if (try_consume_keyword(u"not")) {
                    auto operand = parse_unary();
                    if (!operand) {
                        report(u"not 后缺少操作数。", start, 3);
                        return std::nullopt;
                    }

                    const auto operand_span = span_of(*operand);
                    return make_not(
                        std::move(*operand),
                        source::TextSpan::from_bounds(
                            to_absolute(start), operand_span.end()));
                }

                return parse_primary();
            }

            // primary：括号、变量名或 true/false。
            [[nodiscard]] std::optional<BoolExpr> parse_primary()
            {
                skip_whitespace();

                if (position_ >= text_.size()) {
                    report(u"期望变量名、true、false 或左括号。", position_, 0);
                    return std::nullopt;
                }

                const auto start = position_;
                const auto current = text_[position_];

                if (current == u'(') {
                    ++position_;

                    auto expression = parse_or();
                    skip_whitespace();

                    if (position_ >= text_.size() || text_[position_] != u')') {
                        report(u"缺少右括号。", position_, 0);
                        return expression;
                    }

                    ++position_;
                    return expression;
                }

                if (is_identifier_start(current)) {
                    ++position_;
                    while (position_ < text_.size() && is_identifier_part(text_[position_])) {
                        ++position_;
                    }

                    const auto name = text_.substr(start, position_ - start);
                    if (name == u"and" || name == u"or" || name == u"not") {
                        std::u16string message = u"保留字 ";
                        message += name;
                        message += u" 不能作为变量名。";
                        report(std::move(message), start, name.size());
                        return std::nullopt;
                    }

                    const auto span = source::TextSpan{
                        to_absolute(start), static_cast<std::int32_t>(name.size())};

                    bool value = false;
                    if (try_parse_boolean(name, value)) {
                        return make_bool_literal(value, span);
                    }

                    return make_bool_ref(std::u16string(name), span);
                }

                std::u16string message = u"无法识别的字符 '";
                message.push_back(current);
                message += u"'。";
                report(std::move(message), position_, 1);
                ++position_;
                return std::nullopt;
            }

            // 关键字必须是完整标识符，避免把 "order" 当成 or。
            [[nodiscard]] bool try_consume_keyword(std::u16string_view keyword) noexcept
            {
                if (position_ + keyword.size() > text_.size()) {
                    return false;
                }

                if (text_.compare(position_, keyword.size(), keyword) != 0) {
                    return false;
                }

                const auto end = position_ + keyword.size();
                if (end < text_.size() && is_identifier_part(text_[end])) {
                    return false;
                }

                position_ = end;
                return true;
            }

            void skip_whitespace() noexcept
            {
                while (position_ < text_.size() && is_space(text_[position_])) {
                    ++position_;
                }
            }

            // 局部下标 → 源码绝对偏移，用于诊断和 AST span。
            [[nodiscard]] std::int32_t to_absolute(std::size_t local_position) const noexcept
            {
                return offset_ + static_cast<std::int32_t>(local_position);
            }

            void report(std::u16string message, std::size_t local_position, std::size_t length)
            {
                diagnostics_.error(
                    diagnostics::DiagnosticCode::InvalidCondition,
                    source::TextSpan{
                        to_absolute(local_position),
                        static_cast<std::int32_t>(length)},
                    std::move(message));
            }

            std::u16string_view text_;
            std::int32_t offset_{0};
            diagnostics::DiagnosticBag& diagnostics_;
            std::size_t position_{0};
        };

    } // namespace

    std::optional<BoolExpr> parse_condition(
        std::u16string_view text,
        std::int32_t offset,
        diagnostics::DiagnosticBag& diagnostics)
    {
        if (source::trim(text).empty()) {
            return std::nullopt;
        }

        return ConditionParser(text, offset, diagnostics).parse_all();
    }

} // namespace weftline::syntax
