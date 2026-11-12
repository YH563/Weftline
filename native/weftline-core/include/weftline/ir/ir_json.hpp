#pragma once

#include <string>

#include "weftline/ir/graph_projection.hpp"
#include "weftline/ir/runtime_projection.hpp"
#include "weftline/ir/story_graph.hpp"

namespace weftline::ir {

    /**
     * @brief 把 StoryGraph 序列化为紧凑 JSON。
     *
     * 字段名与 C# 的 camelCase 契约一致，供跨语言传输和 HTML 内嵌。
     */
    [[nodiscard]] std::string story_graph_to_json(const StoryGraph& graph);

    /**
     * @brief 把 GraphProjection 序列化为紧凑 JSON。
     */
    [[nodiscard]] std::string graph_projection_to_json(const GraphProjection& projection);

    /**
     * @brief 把 RuntimeProjection 序列化为播放器 JSON。
     *
     * 输出不包含源码位置或分析结果；字段名与 C# 的 camelCase 契约一致。
     */
    [[nodiscard]] std::string runtime_projection_to_json(
        const RuntimeProjection& projection);

} // namespace weftline::ir
