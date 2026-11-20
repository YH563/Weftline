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
     * @brief 释放 core 返回的文本缓冲区。
     *
     * 适用于 weft_parse_json / weft_project_compile 等所有返回文本的接口。
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
     * core 不访问磁盘：文档文本由宿主通过 weft_project_set_document 传入。
     */
    typedef struct WeftProject WeftProject;

    /**
     * @brief 项目编译选项位。
     *
     * 诊断、符号和项目名始终返回；其他产物按位选择：
     * - WEFT_COMPILE_PARTIAL：有 error 时仍构建部分图；
     * - WEFT_COMPILE_PROJECTION：返回 graph 和 projection；
     * - WEFT_COMPILE_RUNTIME：无 error 时返回 runtime 播放器载荷；
     * - WEFT_COMPILE_WSTORY：返回由 .weft 声明生成的 .wstory 文本。
     *
     * 严格模式是默认；PARTIAL 需要显式开启。
     */
    typedef unsigned int WeftCompileFlags;

    #define WEFT_COMPILE_NONE       0u
    #define WEFT_COMPILE_PARTIAL    (1u << 0)
    #define WEFT_COMPILE_PROJECTION (1u << 1)
    #define WEFT_COMPILE_RUNTIME    (1u << 2)
    #define WEFT_COMPILE_WSTORY     (1u << 3)

    /**
     * @brief 创建一个空项目句柄。
     *
     * @param folder 项目文件夹，仅用于项目名回退；可为 NULL。
     */
    WEFTLINE_API WeftProject* weft_project_create(const char* folder);

    /**
     * @brief 销毁项目句柄；会释放其持有的文档文本。
     */
    WEFTLINE_API void weft_project_destroy(WeftProject* project);

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
     * @brief 编译当前项目并返回 UTF-8 JSON。
     *
     * 响应 schema 见 interop/contract/project.response.schema.json：
     * - 始终包含 schemaVersion、version、projectName、hasErrors、diagnostics、symbols；
     * - flags 含 PROJECTION 时包含 graph 和 projection；
     * - flags 含 RUNTIME 且无 error 时包含 runtime；
     * - flags 含 WSTORY 时包含 wstory（UTF-8 的 .wstory 文本）。
     *
     * @return JSON；由 core 分配，调用方必须用 weft_free() 释放。
     *         仅当响应缓冲区分配失败时返回 NULL。
     */
    WEFTLINE_API char* weft_project_compile(
        WeftProject* project, WeftCompileFlags flags);

#ifdef __cplusplus
}
#endif

#endif
