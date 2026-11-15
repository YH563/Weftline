# 编译与运行

[**中文**](./building.md) | [English](./en/building.md)

本文说明如何从源码构建 native core、桌面端、测试和基准。

---

## 1. 环境要求

- .NET 10 SDK
- CMake 3.20+
- Ninja
- 支持 C++20 的编译器
- Linux 需要 X11 开发库和可用的 OpenGL/EGL 驱动
- Windows 建议在 “x64 Native Tools Command Prompt for VS 2022” 中执行 native 构建

检查环境：

```bash
dotnet --version
cmake --version
ninja --version
```

---

## 2. 构建 native core

```bash
cmake -S native -B native/out/build/release -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DBUILD_TESTING=ON
cmake --build native/out/build/release
```

构建产物：

```text
native/out/build/release/weftline-core/libweftline_core.so   # Linux
native/out/build/release/weftline-core/weftline_core.dll     # Windows
```

`Weftline.Native.csproj` 会把对应的动态库复制到输出目录，App 和 Bench 通过项目引用自动获得它。

如果不需要 native 测试，可以将 `-DBUILD_TESTING=ON` 改为 `OFF`。

---

## 3. 构建桌面端

```bash
dotnet restore Weftline.slnx
dotnet build Weftline.slnx
```

运行：

```bash
dotnet run --project src/Weftline.App
```

Release 构建：

```bash
dotnet build Weftline.slnx -c Release
dotnet run --project src/Weftline.App -c Release
```

---

## 4. 运行测试

```bash
ctest --test-dir native/out/build/release --output-on-failure
dotnet test Weftline.slnx
```

C# 测试项目：

- `tests/Weftline.Core.Tests`：图分析、播放器状态机
- `tests/Weftline.App.Tests`：编辑器、菜单交互、对话框、UI 集成

---

## 5. 运行基准

```bash
dotnet run --project bench/Weftline.Benchmarks -c Release
```

基准覆盖单文件解析、项目编译和编辑器文本快照。

---

## 6. UI 离屏预览

```bash
./preview.sh
./preview.sh --dark
./preview.sh --problems
./preview.sh --preview-panel
./preview.sh --player --advance 5
```

更多参数见 `./preview.sh --help`。

---

## 7. 常见问题

### `DllNotFoundException: weftline_core`

- 先构建 native core。
- 确认 `libweftline_core.so` / `weftline_core.dll` 已复制到 App 输出目录。
- 检查 native 构建目录是否为 `native/out/build/release`；换目录时需要同步 `Weftline.Native.csproj` 中的路径。

### Windows 下 DLL 加载失败

- 确认 native core 与 .NET 进程架构一致（通常都是 x64）。
- 在 “x64 Native Tools Command Prompt for VS 2022” 中重新构建 native core。

### Linux 下界面字体或渲染异常

- 确认安装了中文字体（推荐 `Noto Sans CJK SC`）。
- 可尝试使用 Release 构建。
