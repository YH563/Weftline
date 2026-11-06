#include <gtest/gtest.h>

#include <string>
#include <utility>

#include "weftline/syntax/ast.hpp"
#include "weftline/syntax/ast_printer.hpp"

namespace {

    using namespace weftline;

    TEST(AstPrinter, PrintsMinimalWeftFile)
    {
        syntax::WeftFile file;
        file.title = u"T";
        file.title_span = {0, 1};

        syntax::Module module;
        module.title = u"M";
        module.title_span = {2, 1};

        syntax::Narration narration;
        narration.segments.push_back(syntax::TextLiteral{u"hi ", {0, 3}});
        narration.segments.push_back(syntax::TextReference{u"x", {0, 1}});
        narration.span = {0, 8};
        module.content.push_back(std::move(narration));

        syntax::Exit exit;
        syntax::ExitBranch branch;
        branch.target = u"END";
        branch.target_span = {0, 3};
        exit.branches.push_back(std::move(branch));
        module.exit = std::move(exit);

        file.modules.push_back(std::move(module));

        const std::string expected =
            "WeftFile\n"
            "    Module \"M\"\n"
            "        [hi {x}]\n"
            "        @next auto\n"
            "            -> END\n";

        EXPECT_EQ(syntax::print_ast_utf8(syntax::ScriptFile{std::move(file)}), expected);
    }

    TEST(AstPrinter, PrintsChoiceBranchWithCondition)
    {
        syntax::WeftFile file;

        syntax::Module module;
        module.title = u"M";
        module.title_span = {0, 1};

        syntax::Exit exit;
        exit.character = std::u16string(u"萧影");

        syntax::ExitBranch branch;
        branch.label = std::u16string(u"相信他");
        branch.condition = syntax::make_not(
            syntax::make_bool_ref(u"trust_low", {}), {});
        branch.target = u"END";
        branch.target_span = {0, 3};
        exit.branches.push_back(std::move(branch));
        module.exit = std::move(exit);

        file.modules.push_back(std::move(module));

        const std::string text =
            syntax::print_ast_utf8(syntax::ScriptFile{std::move(file)});

        EXPECT_NE(text.find("        @next choice (萧影)\n"), std::string::npos);
        EXPECT_NE(
            text.find("            [相信他] when not (trust_low) -> END\n"),
            std::string::npos);
    }

} // namespace
