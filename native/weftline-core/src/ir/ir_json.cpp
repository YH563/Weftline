#include "weftline/ir/ir_json.hpp"

#include <string>
#include <string_view>
#include <vector>

#include "weftline/json/json_writer.hpp"
#include "weftline/source/source_text.hpp"

namespace weftline::ir {
    namespace {

        void write_source(
            json::Writer& writer,
            const source::SourceLocation& location)
        {
            writer.begin_object();
            writer.key("filePath").string(location.file_path);
            writer.key("span").begin_object();
            writer.key("start").number(location.span.start);
            writer.key("length").number(location.span.length);
            writer.end_object();
            writer.end_object();
        }

        void write_bool_expr(json::Writer& writer, const IrBoolExpr& expression)
        {
            writer.begin_object();
            writer.key("kind").string(bool_expr_kind_name(expression.kind));

            switch (expression.kind) {
                case BoolExprKind::Literal:
                    writer.key("value").boolean(expression.value);
                    break;
                case BoolExprKind::Ref:
                    if (!expression.name.empty()) {
                        writer.key("name").string(
                            source::utf16_to_utf8(expression.name));
                    }
                    break;
                case BoolExprKind::Not:
                    if (expression.operand) {
                        writer.key("expr");
                        write_bool_expr(writer, *expression.operand);
                    }
                    break;
                case BoolExprKind::And:
                case BoolExprKind::Or:
                    if (expression.left) {
                        writer.key("left");
                        write_bool_expr(writer, *expression.left);
                    }
                    if (expression.right) {
                        writer.key("right");
                        write_bool_expr(writer, *expression.right);
                    }
                    break;
            }

            writer.end_object();
        }

        void write_segment(json::Writer& writer, const IrTextSegment& segment)
        {
            writer.begin_object();
            writer.key("kind").string(text_segment_kind_name(segment.kind));

            if (segment.kind == TextSegmentKind::Text) {
                writer.key("text").string(source::utf16_to_utf8(segment.text));
            } else {
                writer.key("name").string(source::utf16_to_utf8(segment.name));
            }

            writer.end_object();
        }

        void write_content(json::Writer& writer, const IrContentBlock& content)
        {
            writer.begin_object();
            writer.key("kind").string(content_kind_name(content.kind));

            if (content.kind == ContentKind::Narration
                || content.kind == ContentKind::Dialogue) {
                writer.key("segments").begin_array();
                for (const auto& segment : content.segments) {
                    write_segment(writer, segment);
                }
                writer.end_array();
            }

            if (content.kind == ContentKind::Dialogue && content.speaker) {
                writer.key("speaker").string(source::utf16_to_utf8(*content.speaker));
            }

            if (content.kind == ContentKind::Figure && content.path) {
                writer.key("path").string(source::utf16_to_utf8(*content.path));
            }

            writer.end_object();
        }

        void write_effect(json::Writer& writer, const IrAssignment& effect)
        {
            writer.begin_object();
            writer.key("name").string(source::utf16_to_utf8(effect.name));
            writer.key("value").boolean(effect.value);
            writer.end_object();
        }

        void write_branch(json::Writer& writer, const IrExitBranch& branch)
        {
            writer.begin_object();

            if (branch.condition) {
                writer.key("condition");
                write_bool_expr(writer, *branch.condition);
            }
            if (branch.label) {
                writer.key("label").string(source::utf16_to_utf8(*branch.label));
            }

            writer.key("target").string(source::utf16_to_utf8(branch.target));

            writer.key("effects").begin_array();
            for (const auto& effect : branch.effects) {
                write_effect(writer, effect);
            }
            writer.end_array();

            writer.key("source");
            write_source(writer, branch.source);

            writer.end_object();
        }

        void write_exit(json::Writer& writer, const IrExitBlock& exit)
        {
            writer.begin_object();
            writer.key("mode").string(exit_mode_name(exit.mode));

            if (exit.character) {
                writer.key("character").string(source::utf16_to_utf8(*exit.character));
            }

            writer.key("branches").begin_array();
            for (const auto& branch : exit.branches) {
                write_branch(writer, branch);
            }
            writer.end_array();

            writer.key("source");
            write_source(writer, exit.source);

            writer.end_object();
        }

        void write_module(json::Writer& writer, const IrModule& module)
        {
            writer.begin_object();
            writer.key("id").string(source::utf16_to_utf8(module.id));
            writer.key("chapterId").string(source::utf16_to_utf8(module.chapter_id));

            if (module.scene) {
                writer.key("scene").string(source::utf16_to_utf8(*module.scene));
            }

            writer.key("content").begin_array();
            for (const auto& content : module.content) {
                write_content(writer, content);
            }
            writer.end_array();

            if (module.exit) {
                writer.key("exit");
                write_exit(writer, *module.exit);
            }

            writer.key("source");
            write_source(writer, module.source);

            writer.end_object();
        }

        void write_chapter_flags(
            json::Writer& writer, GraphChapterFlags flags)
        {
            writer.key("flags").begin_array();
            if (has_flag(flags, GraphChapterFlags::Entry)) {
                writer.string("entry");
            }
            if (has_flag(flags, GraphChapterFlags::HasIncoming)) {
                writer.string("hasIncoming");
            }
            if (has_flag(flags, GraphChapterFlags::HasOutgoing)) {
                writer.string("hasOutgoing");
            }
            if (has_flag(flags, GraphChapterFlags::HasEndEdge)) {
                writer.string("hasEndEdge");
            }
            writer.end_array();
        }

        void write_module_flags(
            json::Writer& writer, GraphModuleFlags flags)
        {
            writer.key("flags").begin_array();
            if (has_flag(flags, GraphModuleFlags::Entry)) {
                writer.string("entry");
            }
            if (has_flag(flags, GraphModuleFlags::TargetsEnd)) {
                writer.string("targetsEnd");
            }
            if (has_flag(flags, GraphModuleFlags::Unreachable)) {
                writer.string("unreachable");
            }
            if (has_flag(flags, GraphModuleFlags::CannotReachEnd)) {
                writer.string("cannotReachEnd");
            }
            writer.end_array();
        }

        void write_summary(
            json::Writer& writer, const GraphModuleSummary& summary)
        {
            writer.begin_object();
            if (summary.first_line) {
                writer.key("firstLine").string(
                    source::utf16_to_utf8(*summary.first_line));
            }
            writer.key("dialogueCount").number(summary.dialogue_count);
            writer.key("choiceCount").number(summary.choice_count);
            writer.end_object();
        }

        void write_chapter(json::Writer& writer, const GraphChapter& chapter)
        {
            writer.begin_object();
            writer.key("chapterId").string(source::utf16_to_utf8(chapter.chapter_id));
            writer.key("title").string(source::utf16_to_utf8(chapter.title));

            writer.key("moduleIds").begin_array();
            for (const auto& module_id : chapter.module_ids) {
                writer.string(source::utf16_to_utf8(module_id));
            }
            writer.end_array();

            write_chapter_flags(writer, chapter.flags);
            writer.end_object();
        }

        void write_module_node(
            json::Writer& writer, const GraphModuleNode& module)
        {
            writer.begin_object();
            writer.key("moduleId").string(source::utf16_to_utf8(module.module_id));
            writer.key("chapterId").string(source::utf16_to_utf8(module.chapter_id));

            if (module.scene) {
                writer.key("scene").string(source::utf16_to_utf8(*module.scene));
            }

            writer.key("branchCount").number(module.branch_count);
            write_module_flags(writer, module.flags);

            writer.key("summary");
            write_summary(writer, module.summary);

            writer.key("source");
            write_source(writer, module.source);

            writer.end_object();
        }

        void write_module_edge(
            json::Writer& writer, const GraphModuleEdge& edge)
        {
            writer.begin_object();
            writer.key("fromModule").string(source::utf16_to_utf8(edge.from_module));
            writer.key("toModule").string(source::utf16_to_utf8(edge.to_module));
            writer.key("mode").string(exit_mode_name(edge.mode));

            if (edge.label) {
                writer.key("label").string(source::utf16_to_utf8(*edge.label));
            }
            if (edge.condition) {
                writer.key("condition");
                write_bool_expr(writer, *edge.condition);
            }

            writer.key("external").boolean(edge.external);
            writer.key("source");
            write_source(writer, edge.source);
            writer.end_object();
        }

        void write_chapter_edge_detail(
            json::Writer& writer, const ChapterEdgeDetail& detail)
        {
            writer.begin_object();
            writer.key("fromModule").string(source::utf16_to_utf8(detail.from_module));
            writer.key("toModule").string(source::utf16_to_utf8(detail.to_module));
            writer.key("mode").string(exit_mode_name(detail.mode));

            if (detail.label) {
                writer.key("label").string(source::utf16_to_utf8(*detail.label));
            }
            if (detail.condition) {
                writer.key("condition");
                write_bool_expr(writer, *detail.condition);
            }

            writer.key("source");
            write_source(writer, detail.source);
            writer.end_object();
        }

        void write_chapter_edge(json::Writer& writer, const ChapterEdge& edge)
        {
            writer.begin_object();
            writer.key("fromChapter").string(source::utf16_to_utf8(edge.from_chapter));
            writer.key("toChapter").string(source::utf16_to_utf8(edge.to_chapter));
            writer.key("count").number(
                static_cast<std::int64_t>(edge.count()));

            writer.key("details").begin_array();
            for (const auto& detail : edge.details) {
                write_chapter_edge_detail(writer, detail);
            }
            writer.end_array();

            writer.end_object();
        }

    } // namespace

    std::string story_graph_to_json(const StoryGraph& graph)
    {
        json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(graph.schema_version);
        writer.key("end").string(source::utf16_to_utf8(graph.end));

        if (graph.entry) {
            writer.key("entry").string(source::utf16_to_utf8(*graph.entry));
        }

        writer.key("globals").begin_array();
        for (const auto& global : graph.globals) {
            writer.begin_object();
            writer.key("name").string(source::utf16_to_utf8(global.name));
            writer.key("initial").boolean(global.initial);
            writer.end_object();
        }
        writer.end_array();

        writer.key("modules").begin_array();
        for (const auto& module : graph.modules) {
            write_module(writer, module);
        }
        writer.end_array();

        writer.end_object();
        return writer.str();
    }

    std::string graph_projection_to_json(const GraphProjection& projection)
    {
        json::Writer writer;
        writer.begin_object();

        if (projection.entry_module_id) {
            writer.key("entryModuleId").string(
                source::utf16_to_utf8(*projection.entry_module_id));
        }

        writer.key("chapters").begin_array();
        for (const auto& chapter : projection.chapters) {
            write_chapter(writer, chapter);
        }
        writer.end_array();

        writer.key("modules").begin_array();
        for (const auto& module : projection.modules) {
            write_module_node(writer, module);
        }
        writer.end_array();

        writer.key("moduleEdges").begin_array();
        for (const auto& edge : projection.module_edges) {
            write_module_edge(writer, edge);
        }
        writer.end_array();

        writer.key("chapterEdges").begin_array();
        for (const auto& edge : projection.chapter_edges) {
            write_chapter_edge(writer, edge);
        }
        writer.end_array();

        writer.end_object();
        return writer.str();
    }

} // namespace weftline::ir
