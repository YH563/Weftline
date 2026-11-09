cmake_minimum_required(VERSION 3.18)
project(weftline_core)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

# 核心库：source / syntax / diagnostics / api
file(GLOB WEFTLINE_CORE_SOURCES CONFIGURE_DEPENDS
    src/source/*.cpp
    src/syntax/*.cpp
    src/diagnostics/*.cpp
    src/binding/*.cpp
    src/projects/*.cpp
    src/ir/*.cpp
    src/generation/*.cpp
    src/api/*.cpp
)
# 共享库：对外只暴露 core.h 里的 C ABI，供 C# P/Invoke 使用。
add_library(weftline_core SHARED ${WEFTLINE_CORE_SOURCES})

target_include_directories(weftline_core PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>
)
target_compile_definitions(weftline_core PRIVATE WEFTLINE_CORE_BUILD)

if (WIN32)
    # P/Invoke 按 "weftline_core" 解析 DLL 名；MinGW/CMake 默认会加 lib 前缀，
    # 生成 libweftline_core.dll，与 Weftline.App.csproj 期望的路径对不上。
    set_target_properties(weftline_core PROPERTIES PREFIX "")
endif()

# 静态库：给 weftc 和单元测试用它们直接调用 C++ 内部 API。
# Windows 默认只导出 __declspec(dllexport) 的符号，链接共享库会找不到这些内部符号
# （Linux 默认导出全部符号，所以这个问题只在 Windows 上暴露）。
add_library(weftline_core_static STATIC ${WEFTLINE_CORE_SOURCES})

target_include_directories(weftline_core_static PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
    $<INSTALL_INTERFACE:include>
)
target_compile_definitions(weftline_core_static PUBLIC WEFTLINE_CORE_BUILD)

if (MSVC)
    target_compile_options(weftline_core PRIVATE /W4 /permissive-)
else()
    target_compile_options(weftline_core PRIVATE -Wall -Wextra -Wpedantic)
endif()

# 命令行入口
add_executable(weftc src/main.cpp)
target_link_libraries(weftc PRIVATE weftline_core_static)

# 安装规则
install(TARGETS weftline_core
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
    RUNTIME DESTINATION bin
)
install(TARGETS weftc DESTINATION bin)
install(DIRECTORY include/ DESTINATION include)

# 单元测试
if (BUILD_TESTING)
    find_package(GTest REQUIRED)

    add_executable(test_lexer test/test_lexer.cpp)
    target_link_libraries(test_lexer PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_ast test/test_ast.cpp)
    target_link_libraries(test_ast PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_parser test/test_parser.cpp)
    target_link_libraries(test_parser PRIVATE weftline_core_static GTest::gtest_main)
    target_compile_definitions(test_parser PRIVATE
        WEFTLINE_SAMPLES_DIR="${CMAKE_CURRENT_SOURCE_DIR}/../../samples")

    add_executable(test_wstory test/test_wstory.cpp)
    target_link_libraries(test_wstory PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_binder test/test_binder.cpp)
    target_link_libraries(test_binder PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_project test/test_project.cpp)
    target_link_libraries(test_project PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_ir test/test_ir.cpp)
    target_link_libraries(test_ir PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_storygraph test/test_storygraph.cpp)
    target_link_libraries(test_storygraph PRIVATE weftline_core_static GTest::gtest_main)
    target_compile_definitions(test_storygraph PRIVATE
        WEFTLINE_SAMPLES_DIR="${CMAKE_CURRENT_SOURCE_DIR}/../../samples")

    add_executable(test_graph_analysis test/test_graph_analysis.cpp)
    target_link_libraries(test_graph_analysis PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_graph_projection test/test_graph_projection.cpp)
    target_link_libraries(test_graph_projection PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_project_compiler test/test_project_compiler.cpp)
    target_link_libraries(test_project_compiler PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_project_api test/test_project_api.cpp)
    target_link_libraries(test_project_api PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_parse_api test/test_parse_api.cpp)
    target_link_libraries(test_parse_api PRIVATE weftline_core_static GTest::gtest_main)

    add_executable(test_ir_json test/test_ir_json.cpp)
    target_link_libraries(test_ir_json PRIVATE weftline_core_static GTest::gtest_main)

    add_test(NAME test_lexer COMMAND test_lexer)
    add_test(NAME test_ast COMMAND test_ast)
    add_test(NAME test_parser COMMAND test_parser)
    add_test(NAME test_wstory COMMAND test_wstory)
    add_test(NAME test_binder COMMAND test_binder)
    add_test(NAME test_project COMMAND test_project)
    add_test(NAME test_ir COMMAND test_ir)
    add_test(NAME test_storygraph COMMAND test_storygraph)
    add_test(NAME test_graph_analysis COMMAND test_graph_analysis)
    add_test(NAME test_graph_projection COMMAND test_graph_projection)
    add_test(NAME test_project_compiler COMMAND test_project_compiler)
    add_test(NAME test_project_api COMMAND test_project_api)
    add_test(NAME test_parse_api COMMAND test_parse_api)
    add_test(NAME test_ir_json COMMAND test_ir_json)
endif()
