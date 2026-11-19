# 跨语言响应约定

本目录的 JSON Schema 描述 C++ core 返回、C# 侧消费的响应。Schema 之外，还有几条全局约定。

## 版本

- 每个响应顶层都有 `schemaVersion`（当前为 4）。
- 嵌套载荷带各自的版本：`graph` 是 `schemaVersion: 2`（StoryGraph），`runtime` 是 `schemaVersion: 1`（RuntimeProjection）。
- `projection` 目前没有版本字段，见「待定」。

## 数组与可选标量

- **数组 / 映射字段永远出现**，为空时写 `[]` / `{}`。
- **可选标量 / 可选对象缺失即省略**，不写 `null`。
- 两者对 `System.Text.Json` 等价：缺失字段落到 DTO 的默认值（`= []`），显式空数组也落到空列表。

## 所有权

- `weft_parse_json` / `weft_project_compile` 返回的缓冲区由 core 用 `malloc` 分配，调用方必须用 `weft_free` 释放。
- 只有响应缓冲区分配失败时才返回 `NULL`；其它失败都以 JSON 的 `error` + `hasErrors` 表达。

## 待定

- **`graph` 字段**：C# 侧目前只读 `globals`，其完整正文与 `runtime` 重复；是否保留取决于前端是否需要 HTML 离线载荷。在需求明确前冻结该载荷，不再扩展。
- **`projection` 缺少 `schemaVersion`**：与同响应的其它载荷不一致，待与上一条一起决策。
