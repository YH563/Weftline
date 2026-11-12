#pragma once

#include <optional>
#include <string>
#include <vector>

#include "weftline/ir/story_graph.hpp"

namespace weftline::ir {

    /**
     * @brief 播放器使用的章节标题。
     */
    struct RuntimeChapter
    {
        std::u16string id;
        std::u16string title;
    };

    /**
     * @brief 去掉源码位置的出口分支。
     */
    struct RuntimeBranch
    {
        std::optional<IrBoolExpr> condition;
        std::optional<std::u16string> label;
        std::u16string target;
        std::vector<IrAssignment> effects;
    };

    /**
     * @brief 去掉源码位置的出口块。
     */
    struct RuntimeExit
    {
        ExitMode mode{ExitMode::Auto};
        std::optional<std::u16string> character;
        std::vector<RuntimeBranch> branches;
    };

    /**
     * @brief 播放器模块：完整正文与可选出口块。
     */
    struct RuntimeModule
    {
        std::u16string id;
        std::u16string chapter_id;
        std::optional<std::u16string> scene;
        std::vector<IrContentBlock> content;
        std::optional<RuntimeExit> exit;
    };

    /**
     * @brief 剧本预览与 HTML 导出共用的播放器载荷。
     *
     * 不包含源码位置、诊断、分析结果或 .wstory 设计记录；章节/模块/分支顺序
     * 与 StoryGraph 保持一致，供播放器按确定顺序执行。
     */
    struct RuntimeProjection
    {
        int schema_version{1};
        std::u16string project_name;
        std::optional<std::u16string> entry;
        std::u16string end{u"END"};
        std::vector<RuntimeChapter> chapters;
        std::vector<IrGlobal> globals;
        std::vector<RuntimeModule> modules;
    };

} // namespace weftline::ir
