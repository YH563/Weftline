# Weftline Native 代码规范

适用于 `native/` 下的 C++、CMake 和测试代码。
目标：**可移植、可读、可对比迁移**。迁移期以 C# 实现为参考，但代码风格以本规范为准。

---

## 1. 目录与文件

```
native/
├── weftline-core/
│   ├── include/weftline/<module>/*.hpp    # 公开头 + 内部头
│   ├── src/<module>/*.cpp                 # 与 include 镜像
│   ├── src/main.cpp                       # 可执行入口
│   └── test/test_*.cpp                    # GTest
└── CMakeLists.txt / CMakePresets.json
```

- 文件名用 `snake_case`。
- C++ 头文件用 `.hpp`；对外 C ABI 用 `.h`（例如 `core.h`），因为它要能被 C/FFI 使用。
- 内部头文件必须用 `#pragma once`；`core.h` 使用 include guard。
- 一个 `.cpp` 对应一个同名头文件（`lexer.cpp` ↔ `lexer.hpp`）。
- 不要在新的目录层级里重复 `weftline/` 前缀；`include/weftline/` 已经提供了命名空间根。

---

## 2. 命名

| 类别 | 规则 | 示例 |
|---|---|---|
| 命名空间 | 小写，`::` 分层 | `weftline::syntax` |
| 类型（class/struct/enum） | PascalCase | `SourceText`、`TokenKind`、`BoolExpr` |
| 函数 / 方法 | snake_case | `tokenize`、`get_line_position` |
| 工厂函数 | `make_` 前缀 | `make_bool_ref`、`make_and` |
| 谓词 | `is_` / `has_` 前缀 | `is_empty`、`has_value` |
| 变量 / 参数 | snake_case | `source_utf8`、`file_path` |
| 私有数据成员 | snake_case + 尾随 `_` | `text_`、`lines_`、`out_` |
| 编译期常量 | `kPascalCase` | `kMaxDepth`、`kSchemaVersion` |
| 宏 | `WEFTLINE_` 前缀 + 大写 | `WEFTLINE_API` |
| 枚举值 | PascalCase | `TokenKind::AtKeyword`、`Severity::Error` |

- 不要用匈牙利命名法。
- 不要用单字母名字，循环下标 `i/j` 除外。
- 布尔变量/返回值用 `is_` / `has_` / `can_` 前缀。

---

## 3. 格式

- 4 空格缩进，**不使用 tab**。
- UTF-8、LF、文件末尾保留一个换行、行尾不留空白。
- 行宽尽量不超过 100 列。
- `namespace` 内容缩进一级；嵌套匿名 `namespace` 再缩进一级。
- `extern "C"` 内容缩进一级。
- 类型和函数使用 Allman（左大括号另起一行）：

  ```cpp
  struct TextSpan
  {
  };

  std::u16string get_text(TextSpan span) const
  {
  }
  ```

- 控制语句使用 K&R（左大括号同一行），`else` / `catch` 紧跟在 `}` 后：

  ```cpp
  if (condition) {
      ...
  } else {
      ...
  }
  ```

- 不要用装饰性分隔线注释：

  ```cpp
  // ── 范围辅助 ─────────────────────────────  // 禁止
  // ======================================  // 禁止
  // ******** Section ********              // 禁止
  ```

  需要分组时用空行，必要时用一句普通 `//` 说明，不要画线。

- include 顺序：本文件头、空行、标准库、空行、项目头。不要依赖隐式 include。

---

## 4. 注释规范

### 4.1 禁止的注释

- 装饰性分隔线（`// ──`、`// ====`、`// ****` 等）。
- 复述代码的注释：`i++; // i 加一`。
- 注释掉的旧代码；要保留历史请用 git。
- 无主 TODO：`// TODO: 以后再说`。必须写成 `// TODO(name): 具体事项`。
- 行尾噪声注释；只有非常短的约束说明才允许行尾注释。

### 4.2 必须添加注释的位置

以下位置**必须**有注释，缺了视为未完成：

1. **头文件中的每个公开类型**
   - `struct` / `class` / `enum` / `using` 别名。
   - 用 Doxygen `/** @brief ... */` 说明职责和关键不变量。

2. **头文件中的每个公开函数/方法**
   - 包括构造/工厂/自由函数。
   - 有参数用 `@param`，有返回值用 `@return`，有所有权/生命周期/编码约束用 `@note` 或 `@warning`。

3. **C ABI 的每个导出函数**
   - 必须写清楚：输入/输出编码、内存所有权、失败时的返回值、是否需要 `weft_free`。
   - 示例见 `include/weftline/core.h`。

4. **容易踩坑的实现块**
   - UTF-8 / UTF-16 转换、代理对、offset 语义。
   - 内存分配/释放配对、所有权转移。
   - 跨语言边界、JSON schema/版本、线程安全。
   - 二分查找、递归、图遍历、缓存失效等非直观算法。
   - 平台/编译器 workaround、性能取舍。

5. **允许失败/部分结果的路径**
   - 解析器“即使有错也返回部分 AST”这类行为必须说明。

6. **新增的 TODO / FIXME / HACK**
   - 必须带责任人或 issue：`// TODO(name): ...`、`// FIXME(name): ...`。

### 4.3 不需要注释的位置

- 自解释的 getter/setter、简单转发、直接返回成员。
- 与函数名/签名完全重复的说明。
- 显而易见的循环和赋值。
- 空行已经能表达分组的地方。

### 4.4 接口注释模板

```cpp
/**
 * @brief 一句话说明职责。
 *
 * 可选：补充关键不变量、失败行为、线程/生命周期约束。
 */
struct Token
{
    ...
};

/**
 * @brief 把 UTF-16 偏移换算成 1 基行列位置。
 *
 * @param position UTF-16 code unit 偏移，越界会被 clamp。
 * @return 1 基的行号和列号。
 */
[[nodiscard]] LinePosition get_line_position(std::int32_t position) const;
```

### 4.5 实现内小注释

- 用 `//`，写在被解释代码块的上一行。
- 只解释“为什么”和“边界条件”，不解释“做了什么”。
- 同一个函数里不要每行都加注释；只标关键决策点。

```cpp
// 二分查找最后一个 start <= position 的行。
const auto found = std::ranges::upper_bound(...);
```

### 4.6 语言

- 注释用中文，代码标识符、类型名、命令、协议字段保持英文。
- 对外 C ABI 如有必要可中英双语，但同一段不要混排过多。
- 专有名词保留英文：UTF-16、JSON、NAPI、P/Invoke。

---

## 5. 类型与所有权

- 递归 AST 节点用 `support::Box<T>`（当前是 `std::unique_ptr`），不要裸 `new/delete`。
- AST 自己拥有字符串（`std::u16string`）；`TextSpan` 的偏移统一是 **UTF-16 code unit**。
- 可空用 `std::optional`；联合类型用 `std::variant`。
- 不要返回指向局部对象的 `string_view` / 指针。
- 对外 C ABI 的返回值所有权必须在头文件里写清楚。

---

## 6. 错误处理

- 用户输入错误不用异常，收集到 `Diagnostic` / `DiagnosticBag`。
- 解析失败时返回“当前可用的最佳 AST + 诊断”，不要直接抛。
- C ABI 边界必须 `try/catch (...)` 兜底，异常不能跨语言传播。
- 不要把 `assert` 当运行时错误处理；`assert` 只用于内部不变量。

---

## 7. C++20 使用范围

推荐：

- `std::variant` / `std::optional` / `std::string_view` / `std::span`
- concepts、`constexpr` / `consteval`、`<=>`、designated initializers
- `std::ranges`（确认目标工具链支持）

避免：

- `modules`（各平台支持不一致）。
- `std::format`（鸿蒙 NDK 支持待验证）。
- 编译器扩展、GNU 专属写法。
- 未验证支持的第三方依赖。

新增特性前，至少在桌面 GCC/Clang 和 OH NDK 上各编译一次。

---

## 8. 测试

- 使用 GTest，文件放 `test/test_<module>.cpp`。
- 测试名描述行为：`TEST(BoolExpr, PrecedenceAndBeforeOr)`。
- 一个测试只验证一个行为；失败信息要能定位问题。
- 迁移期优先做**差分/golden 测试**：C# 输出与 C++ 输出逐字段对比。
- 测试不要依赖当前工作目录；路径从 CMake/参数传入。

---

## 9. CMake

- 每个包一个 `CMakeLists.txt`。
- 源文件用 `file(GLOB ... CONFIGURE_DEPENDS)`（与 RUS_Engine 风格一致）。
- 共享库 + 可执行 + `install()` 规则；测试放在 `if (BUILD_TESTING)`。
- 不要在 CMake 里用 `# ──` 分隔线；用普通 `# 说明` 或空行。

---

## 10. 提交前检查清单

- [ ] 没有装饰性分隔线注释。
- [ ] 新增的公开类型/函数都有 Doxygen 注释。
- [ ] 编码、所有权、跨语言边界、复杂算法有说明。
- [ ] 没有注释掉的旧代码。
- [ ] TODO/FIXME 带责任人。
- [ ] `cmake --build --preset debug` 通过。
- [ ] `ctest --preset debug` 通过。
- [ ] 新增行为有对应测试。
