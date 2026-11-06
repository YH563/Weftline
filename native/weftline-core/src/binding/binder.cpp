#include "weftline/binding/binder.hpp"

#include <optional>
#include <string>
#include <string_view>
#include <unordered_set>
#include <utility>
#include <vector>

#include "weftline/support/overloaded.hpp"

namespace weftline::binding {
    namespace {

        void validate_segments(
            const std::vector<syntax::TextSegment>& segments,
            const SymbolTable& symbols,
            diagnostics::DiagnosticBag& diagnostics)
        {
            for (const auto& segment : segments) {
                std::visit(
                    support::overloaded{
                        [](const syntax::TextLiteral&) {},
                        [&](const syntax::TextReference& reference) {
                            if (!symbols.has_character(reference.name)
                                && !symbols.has_item(reference.name)) {
                                std::u16string message = u"{";
                                message += reference.name;
                                message += u"} 引用了未声明的角色或道具。";
                                diagnostics.error(
                                    diagnostics::DiagnosticCode::UndeclaredReference,
                                    reference.span,
                                    std::move(message));
                            }
                        },
                    },
                    segment);
            }
        }

        void validate_condition(
            const syntax::BoolExpr& expression,
            const SymbolTable& symbols,
            diagnostics::DiagnosticBag& diagnostics)
        {
            std::visit(
                support::overloaded{
                    [](const syntax::BoolLiteral&) {},
                    [&](const syntax::BoolRef& reference) {
                        if (!symbols.has_variable(reference.name)) {
                            std::u16string message = u"条件引用了未声明变量：";
                            message += reference.name;
                            diagnostics.error(
                                diagnostics::DiagnosticCode::UndeclaredConditionVariable,
                                reference.span,
                                std::move(message));
                        }
                    },
                    [&](const syntax::NotExpr& expr) {
                        validate_condition(*expr.operand, symbols, diagnostics);
                    },
                    [&](const syntax::AndExpr& expr) {
                        validate_condition(*expr.left, symbols, diagnostics);
                        validate_condition(*expr.right, symbols, diagnostics);
                    },
                    [&](const syntax::OrExpr& expr) {
                        validate_condition(*expr.left, symbols, diagnostics);
                        validate_condition(*expr.right, symbols, diagnostics);
                    },
                },
                expression.node);
        }

    } // namespace

    std::string_view duplicate_code(SymbolKind kind) noexcept
    {
        switch (kind) {
            case SymbolKind::Variable:
                return diagnostics::DiagnosticCode::DuplicateGlobalVariable;
            case SymbolKind::Character:
                return diagnostics::DiagnosticCode::DuplicateCharacter;
            case SymbolKind::Item:
                return diagnostics::DiagnosticCode::DuplicateItem;
            case SymbolKind::Scene:
                return diagnostics::DiagnosticCode::DuplicateScene;
        }
        return diagnostics::DiagnosticCode::DuplicateSymbol;
    }

    std::u16string kind_name(SymbolKind kind)
    {
        switch (kind) {
            case SymbolKind::Variable:
                return u"变量";
            case SymbolKind::Character:
                return u"角色";
            case SymbolKind::Item:
                return u"道具";
            case SymbolKind::Scene:
                return u"场景";
        }
        return u"符号";
    }

    bool declare(
        SymbolTable& symbols,
        std::u16string name,
        SymbolKind kind,
        std::optional<bool> value,
        source::TextSpan span,
        std::string_view file_path,
        diagnostics::DiagnosticBag& diagnostics)
    {
        SymbolEntry entry{
            std::move(name),
            kind,
            source::SourceLocation{std::string(file_path), span},
            value,
        };

        const auto entry_name = entry.name;
        if (symbols.try_declare(std::move(entry))) {
            return true;
        }

        const auto* existing = symbols.find(entry_name);
        std::u16string message;
        std::string_view code;

        if (existing != nullptr && existing->kind == kind) {
            code = duplicate_code(kind);
            message = u"全局";
            message += kind_name(kind);
            message += u"重复声明：";
            message += entry_name;
        } else {
            code = diagnostics::DiagnosticCode::DuplicateSymbol;
            message = u"符号名已被声明为";
            message += existing != nullptr ? kind_name(existing->kind) : u"符号";
            message += u"：";
            message += entry_name;
        }

        diagnostics.error(code, span, std::move(message));
        return false;
    }

    void bind_module(
        const syntax::Module& module,
        SymbolTable& symbols,
        diagnostics::DiagnosticBag& diagnostics)
    {
        for (const auto& content : module.content) {
            std::visit(
                support::overloaded{
                    [&](const syntax::Narration& narration) {
                        validate_segments(narration.segments, symbols, diagnostics);
                    },
                    [&](const syntax::Dialogue& dialogue) {
                        if (!symbols.has_character(dialogue.speaker)) {
                            std::u16string message = u"对话引用了未声明的角色：";
                            message += dialogue.speaker;
                            diagnostics.error(
                                diagnostics::DiagnosticCode::UndeclaredCharacter,
                                dialogue.span,
                                std::move(message));
                        }
                        validate_segments(dialogue.segments, symbols, diagnostics);
                    },
                    [](const syntax::Figure&) {},
                },
                content);
        }

        if (!module.exit) {
            return;
        }

        const auto& exit = *module.exit;
        if (exit.character && !symbols.has_character(*exit.character)) {
            std::u16string message = u"出口引用了未声明的角色：";
            message += *exit.character;
            diagnostics.error(
                diagnostics::DiagnosticCode::UndeclaredCharacter,
                exit.span,
                std::move(message));
        }

        for (const auto& branch : exit.branches) {
            if (branch.condition) {
                validate_condition(*branch.condition, symbols, diagnostics);
            }

            for (const auto& effect : branch.effects) {
                if (!symbols.has_variable(effect.name)) {
                    std::u16string message = u"分支效果引用了未声明变量：";
                    message += effect.name;
                    diagnostics.error(
                        diagnostics::DiagnosticCode::UndeclaredVariable,
                        effect.span,
                        std::move(message));
                }
            }
        }
    }

    void bind(
        const syntax::WeftFile& file,
        diagnostics::DiagnosticBag& diagnostics,
        std::string_view file_path)
    {
        SymbolTable symbols;

        for (const auto& variable : file.variables) {
            declare(
                symbols, variable.name, SymbolKind::Variable,
                variable.value, variable.span, file_path, diagnostics);
        }
        for (const auto& character : file.characters) {
            declare(
                symbols, character.name, SymbolKind::Character,
                std::nullopt, character.span, file_path, diagnostics);
        }
        for (const auto& item : file.items) {
            declare(
                symbols, item.name, SymbolKind::Item,
                std::nullopt, item.span, file_path, diagnostics);
        }
        for (const auto& scene : file.scenes) {
            declare(
                symbols, scene.name, SymbolKind::Scene,
                std::nullopt, scene.span, file_path, diagnostics);
        }

        for (const auto& module : file.modules) {
            bind_module(module, symbols, diagnostics);
        }
    }

    void bind(
        const syntax::WStoryFile& file,
        diagnostics::DiagnosticBag& diagnostics,
        std::string_view file_path)
    {
        SymbolTable symbols;
        std::unordered_set<std::u16string> chapter_titles;

        for (const auto& chapter : file.chapters) {
            if (!chapter_titles.insert(chapter.title).second) {
                std::u16string message = u"章节标题重复：";
                message += chapter.title;
                diagnostics.error(
                    diagnostics::DiagnosticCode::DuplicateChapterTitle,
                    chapter.title_span,
                    std::move(message));
            }
        }

        for (const auto& variable : file.variables) {
            declare(
                symbols, variable.name, SymbolKind::Variable,
                variable.value, variable.span, file_path, diagnostics);
        }
        for (const auto& character : file.characters) {
            declare(
                symbols, character.name, SymbolKind::Character,
                std::nullopt, character.span, file_path, diagnostics);
        }
        for (const auto& item : file.items) {
            declare(
                symbols, item.name, SymbolKind::Item,
                std::nullopt, item.span, file_path, diagnostics);
        }
        for (const auto& scene : file.scenes) {
            declare(
                symbols, scene.name, SymbolKind::Scene,
                std::nullopt, scene.span, file_path, diagnostics);
        }
    }

} // namespace weftline::binding
