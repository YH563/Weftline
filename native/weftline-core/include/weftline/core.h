#ifndef WEFTLINE_CORE_H
#define WEFTLINE_CORE_H

#if defined(_WIN32)
#  if defined(WEFTLINE_CORE_BUILD)
#    define WEFTLINE_API __declspec(dllexport)
#  else
#    define WEFTLINE_API __declspec(dllimport)
#  endif
#else
#  define WEFTLINE_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

    /**
     * @brief 返回 core 的版本号。
     *
     * 返回静态字符串，调用方不得释放。
     */
    WEFTLINE_API const char* weft_version(void);

    /**
     * @brief 编译一份源码并返回 UTF-8 JSON。
     *
     * @param request_json 请求 JSON，结构见 interop/contract/compile.request.schema.json。
     * @return 响应 JSON；由 core 分配，调用方必须用 weft_free() 释放。
     *
     * 响应中的 start/length 以 UTF-16 code unit 计量，与 C#/ArkTS 字符串偏移一致。
     * 仅当响应缓冲区分配失败时返回 NULL。
     */
    WEFTLINE_API char* weft_compile_json(const char* request_json);

    /**
     * @brief 释放 core 返回的文本缓冲区。
     *
     * 适用于 weft_compile_json / weft_parse_json / weft_project_compile。
     */
    WEFTLINE_API void weft_free(char* pointer);

    /**
     * @brief 解析单个脚本文件并返回 UTF-8 JSON 诊断。
     *
     * 只做 lexer/parser，返回语法诊断；语义、符号和图仍由项目编译负责。
     * start/length 以 UTF-16 code unit 计量，与 C#/ArkTS 字符串偏移一致。
     *
     * @param path 文件路径，仅用于回显与诊断定位。
     * @param text UTF-8 源码文本；NULL 视为空文本。
     * @param version 调用方的文档版本号，原样回显。
     * @return JSON；由 core 分配，调用方必须用 weft_free() 释放。
     *         仅当响应缓冲区分配失败时返回 NULL。
     */
    WEFTLINE_API char* weft_parse_json(
        const char* path, const char* text, long long version);

    /**
     * @brief 不透明项目句柄，用于实时诊断。
     *
     * 单个句柄不是线程安全的；请在同一线程内创建、更新和编译。
     */
    typedef struct WeftProject WeftProject;

    /**
     * @brief 创建一个空项目句柄。
     */
    WEFTLINE_API WeftProject* weft_project_create(void);

    /**
     * @brief 销毁项目句柄；会释放其持有的文档文本。
     */
    WEFTLINE_API void weft_project_destroy(WeftProject* project);

    /**
     * @brief 设置项目文件夹（仅用于显示/项目名回退）。
     */
    WEFTLINE_API void weft_project_set_folder(WeftProject* project, const char* folder);

    /**
     * @brief 添加或覆盖一个文档。
     *
     * @param path 文档标识，通常是相对/绝对路径；core 不会用它访问磁盘。
     * @param text UTF-8 源码文本。
     */
    WEFTLINE_API void weft_project_set_document(
        WeftProject* project, const char* path, const char* text);

    /**
     * @brief 移除一个文档；不存在时为空操作。
     */
    WEFTLINE_API void weft_project_remove_document(WeftProject* project, const char* path);

    /**
     * @brief 编译当前项目并返回 UTF-8 JSON 诊断。
     *
     * @param allow_partial 有 error 时是否仍构建图和投影。
     * @param build_projection 是否在响应中附带 GraphProjection；关闭时仍返回诊断和符号。
     * @return JSON；由 core 分配，调用方必须用 weft_free() 释放。
     */
    WEFTLINE_API char* weft_project_compile(
        WeftProject* project, int allow_partial, int build_projection);

    /**
     * @brief 编译当前项目并返回 JSON，可选择附带播放器载荷。
     *
     * 与 weft_project_compile 相同，只是多一个 build_runtime 参数：
     * - build_runtime != 0 且无 error 时，响应附带 runtime 字段；
     * - 有 error 时不构建 runtime，调用方应保留上一次可用载荷。
     *
     * @return JSON；由 core 分配，调用方必须用 weft_free() 释放。
     *         仅当响应缓冲区分配失败时返回 NULL。
     */
    WEFTLINE_API char* weft_project_compile_ex(
        WeftProject* project,
        int allow_partial,
        int build_projection,
        int build_runtime);

    /**
     * @brief 编译当前项目并返回播放器载荷 JSON。
     *
     * 只做严格编译（有 error 时不产出载荷），复用项目句柄内的编译缓存。
     * 响应结构：
     *   { "schemaVersion": 1, "hasErrors": false, "runtime": { ... } }
     * 失败时：
     *   { "schemaVersion": 1, "hasErrors": true, "error": "...",
     *     "diagnosticCount": N }
     *
     * runtime 不含源码位置、诊断或 .wstory 设计记录，供剧本预览和 HTML 导出使用。
     *
     * @return JSON；由 core 分配，调用方必须用 weft_free() 释放。
     *         仅当响应缓冲区分配失败时返回 NULL。
     */
    WEFTLINE_API char* weft_project_runtime(WeftProject* project);

#ifdef __cplusplus
}
#endif

#endif
