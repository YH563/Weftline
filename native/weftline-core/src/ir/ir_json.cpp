#include "weftline/ir/ir_json.hpp"

#include <algorithm>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#include "weftline/json/json_writer.hpp"
#include "weftline/source/source_text.hpp"

namespace weftline::ir {
    namespace {

        // 所有源码位置统一写成 { filePath, span: { start, length } }，
        // start/length 是 UTF-16 code unit。
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

        // IR 布尔表达式按 kind 递归输出；子节点是 shared_ptr，可安全共享。
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

        // 正文片段只有两种：text 或 ref（{名字}）。
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

        // 内容块：旁白/对话带 segments，对话额外带 speaker，图片带 path。
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

        // @set 效果统一为 name + boolean value。
        void write_effect(json::Writer& writer, const IrAssignment& effect)
        {
            writer.begin_object();
            writer.key("name").string(source::utf16_to_utf8(effect.name));
            writer.key("value").boolean(effect.value);
            writer.end_object();
        }

        // 出口分支：可选条件/文案、目标、效果数组。
        // StoryGraph 与播放器两种载荷字段一致，只有 source 是否存在不同；
        // 用同一个模板生成，两份输出不会再各自漂移。
        template <typename Branch>
        void write_branch(json::Writer& writer, const Branch& branch)
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

            if constexpr (requires { branch.source; }) {
                writer.key("source");
                write_source(writer, branch.source);
            }

            writer.end_object();
        }

        // 出口块：模式、可选角色、分支列表；播放器版没有源码位置。
        template <typename Exit>
        void write_exit(json::Writer& writer, const Exit& exit)
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

            if constexpr (requires { exit.source; }) {
                writer.key("source");
                write_source(writer, exit.source);
            }

            writer.end_object();
        }

        // 模块完整展开正文和出口；StoryGraph 版带源码位置，播放器版不带。
        template <typename Module>
        void write_module(json::Writer& writer, const Module& module)
        {
            writer.begin_object();
            writer.key("id").string(source::utf16_to_utf8(module.id));
            writer.key("chapterId").string(source::utf16_to_utf8(module.chapter_id));

            writer.key("scenes").begin_array();
            for (const auto& scene : module.scenes) {
                writer.begin_object();
                writer.key("name").string(source::utf16_to_utf8(scene.name));
                writer.key("contentIndex").number(
                    static_cast<std::int64_t>(scene.content_index));
                writer.end_object();
            }
            writer.end_array();

            writer.key("content").begin_array();
            for (const auto& content : module.content) {
                write_content(writer, content);
            }
            writer.end_array();

            if (module.exit) {
                writer.key("exit");
                write_exit(writer, *module.exit);
            }

            if constexpr (requires { module.source; }) {
                writer.key("source");
                write_source(writer, module.source);
            }

            writer.end_object();
        }

        // 角标用字符串数组输出，避免 C# 依赖位值。
        void write_chapter_flags(
            json::Writer& writer, GraphChapterFlags flags)
        {
            writer.key("flags").begin_array();
            if (has_flag(flags, GraphChapterFlags::Entry)) {
                writer.string("entry");
            }
            if (has_flag(flags, GraphChapterFlags::EntryCandidate)) {
                writer.string("entryCandidate");
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
            if (has_flag(flags, GraphModuleFlags::EntryCandidate)) {
                writer.string("entryCandidate");
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

        // 悬停摘要：首行可选，对话/选项数量始终存在。
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

        // 章节容器：ID、标题、模块顺序和角标。
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

        // 模块节点：图视图显示所需的元数据，不含正文。
        void write_module_node(
            json::Writer& writer, const GraphModuleNode& module)
        {
            writer.begin_object();
            writer.key("moduleId").string(source::utf16_to_utf8(module.module_id));
            writer.key("chapterId").string(source::utf16_to_utf8(module.chapter_id));

            writer.key("scenes").begin_array();
            for (const auto& scene : module.scenes) {
                writer.string(source::utf16_to_utf8(scene));
            }
            writer.end_array();

            writer.key("branchCount").number(module.branch_count);
            write_module_flags(writer, module.flags);

            writer.key("summary");
            write_summary(writer, module.summary);

            writer.key("source");
            write_source(writer, module.source);

            writer.end_object();
        }

        // 模块边（GraphModuleEdge）与章节边明细（ChapterEdgeDetail）字段一致，
        // 只有 external 是否存在不同；同一个模板覆盖两种载荷。
        template <typename Edge>
        void write_edge(json::Writer& writer, const Edge& edge)
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

            if constexpr (requires { edge.external; }) {
                writer.key("external").boolean(edge.external);
            }

            writer.key("source");
            write_source(writer, edge.source);
            writer.end_object();
        }

        // 章节边按源/目标章节聚合，details 保留底层模块对。
        void write_chapter_edge(json::Writer& writer, const ChapterEdge& edge)
        {
            writer.begin_object();
            writer.key("fromChapter").string(source::utf16_to_utf8(edge.from_chapter));
            writer.key("toChapter").string(source::utf16_to_utf8(edge.to_chapter));
            writer.key("count").number(
                static_cast<std::int64_t>(edge.count()));

            writer.key("details").begin_array();
            for (const auto& detail : edge.details) {
                write_edge(writer, detail);
            }
            writer.end_array();

            writer.end_object();
        }

        // 引用索引：名字 → 模块 ID 列表，供 C# 筛选面板反查。
        // unordered_map 的遍历顺序不稳定（跨 STL/插入历史不同），这里按名字排序输出，
        // 保证同一份数据逐字节一致，golden/差分对比才有意义。
        void write_reference_map(
            json::Writer& writer,
            const std::unordered_map<std::u16string, std::vector<std::u16string>>& map)
        {
            using Entry = std::pair<const std::u16string, std::vector<std::u16string>>;

            std::vector<const Entry*> entries;
            entries.reserve(map.size());
            for (const auto& item : map) {
                entries.push_back(&item);
            }

            std::sort(
                entries.begin(),
                entries.end(),
                [](const Entry* left, const Entry* right) {
                    return left->first < right->first;
                });

            writer.begin_object();

            for (const auto* entry : entries) {
                writer.key(source::utf16_to_utf8(entry->first)).begin_array();
                for (const auto& module_id : entry->second) {
                    writer.string(source::utf16_to_utf8(module_id));
                }
                writer.end_array();
            }

            writer.end_object();
        }

        // 四类引用并列输出；没有引用的类别输出空对象。
        void write_references(json::Writer& writer, const ReferenceIndex& references)
        {
            writer.begin_object();
            writer.key("characters");
            write_reference_map(writer, references.characters);
            writer.key("items");
            write_reference_map(writer, references.items);
            writer.key("scenes");
            write_reference_map(writer, references.scenes);
            writer.key("variables");
            write_reference_map(writer, references.variables);
            writer.end_object();
        }

        // 播放器分支、出口、模块与 StoryGraph 版共用 write_branch / write_exit /
        // write_module：模板里没有 source 成员时自动不写源码位置。

    } // namespace

    // StoryGraph JSON 用于调试和 HTML 载荷，包含完整正文。
    std::string story_graph_to_json(const StoryGraph& graph)
    {
        json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(graph.schema_version);
        writer.key("end").string(source::utf16_to_utf8(graph.end));

        if (graph.entry) {
            writer.key("entry").string(source::utf16_to_utf8(*graph.entry));
        }

        writer.key("entryCandidates").begin_array();
        for (const auto& candidate : graph.entry_candidates) {
            writer.string(source::utf16_to_utf8(candidate));
        }
        writer.end_array();

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

    // RuntimeProjection JSON 只携带播放器需要的数据；源码位置已在构建阶段丢弃。
    std::string runtime_projection_to_json(const RuntimeProjection& projection)
    {
        json::Writer writer;
        writer.begin_object();
        writer.key("schemaVersion").number(projection.schema_version);
        writer.key("projectName").string(
            source::utf16_to_utf8(projection.project_name));
        writer.key("end").string(source::utf16_to_utf8(projection.end));

        if (projection.entry) {
            writer.key("entry").string(
                source::utf16_to_utf8(*projection.entry));
        }

        writer.key("chapters").begin_array();
        for (const auto& chapter : projection.chapters) {
            writer.begin_object();
            writer.key("id").string(source::utf16_to_utf8(chapter.id));
            writer.key("title").string(source::utf16_to_utf8(chapter.title));
            writer.end_object();
        }
        writer.end_array();

        writer.key("globals").begin_array();
        for (const auto& global : projection.globals) {
            writer.begin_object();
            writer.key("name").string(source::utf16_to_utf8(global.name));
            writer.key("initial").boolean(global.initial);
            writer.end_object();
        }
        writer.end_array();

        writer.key("modules").begin_array();
        for (const auto& module : projection.modules) {
            write_module(writer, module);
        }
        writer.end_array();

        writer.end_object();
        return writer.str();
    }

    // GraphProjection JSON 是图视图的压缩载荷：不含完整正文。
    std::string graph_projection_to_json(const GraphProjection& projection)
    {
        json::Writer writer;
        writer.begin_object();

        if (projection.entry_module_id) {
            writer.key("entryModuleId").string(
                source::utf16_to_utf8(*projection.entry_module_id));
        }

        writer.key("entryCandidates").begin_array();
        for (const auto& candidate : projection.entry_candidates) {
            writer.string(source::utf16_to_utf8(candidate));
        }
        writer.end_array();

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
            write_edge(writer, edge);
        }
        writer.end_array();

        writer.key("chapterEdges").begin_array();
        for (const auto& edge : projection.chapter_edges) {
            write_chapter_edge(writer, edge);
        }
        writer.end_array();

        writer.key("references");
        write_references(writer, projection.references);

        writer.end_object();
        return writer.str();
    }

} // namespace weftline::ir
