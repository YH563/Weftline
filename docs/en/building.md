# Build and run

[中文](../building.md) | [**English**](./building.md)

This page describes how to build the native core, desktop app, tests, and benchmarks from source.

---

## 1. Requirements

- .NET 10 SDK
- CMake 3.20+
- Ninja
- A C++20-capable compiler
- Linux: X11 development libraries and a working OpenGL/EGL driver
- Windows: run the native build from “x64 Native Tools Command Prompt for VS 2022”

Check the environment:

```bash
dotnet --version
cmake --version
ninja --version
```

---

## 2. Build the native core

```bash
cmake -S native -B native/out/build/release -G Ninja \
  -DCMAKE_BUILD_TYPE=Release \
  -DBUILD_TESTING=ON
cmake --build native/out/build/release
```

Artifacts:

```text
native/out/build/release/weftline-core/libweftline_core.so   # Linux
native/out/build/release/weftline-core/weftline_core.dll     # Windows
```

`Weftline.Native.csproj` copies the library to the output directory. App and Bench receive it through project references.

Set `-DBUILD_TESTING=OFF` if native tests are not needed.

---

## 3. Build the desktop app

```bash
dotnet restore Weftline.slnx
dotnet build Weftline.slnx
```

Run:

```bash
dotnet run --project src/Weftline.App
```

Release build:

```bash
dotnet build Weftline.slnx -c Release
dotnet run --project src/Weftline.App -c Release
```

---

## 4. Tests

```bash
ctest --test-dir native/out/build/release --output-on-failure
dotnet test Weftline.slnx
```

C# test projects:

- `tests/Weftline.Core.Tests`: graph analysis and player state machine
- `tests/Weftline.App.Tests`: editor, menu interaction, dialogs, UI integration

---

## 5. Benchmarks

```bash
dotnet run --project bench/Weftline.Benchmarks -c Release
```

Benchmarks cover single-file parsing, project compilation, and editor text snapshots.

---

## 6. Headless UI preview

```bash
./preview.sh
./preview.sh --dark
./preview.sh --problems
./preview.sh --preview-panel
./preview.sh --player --advance 5
```

See `./preview.sh --help` for more options.

---

## 7. Troubleshooting

### `DllNotFoundException: weftline_core`

- Build the native core first.
- Make sure `libweftline_core.so` / `weftline_core.dll` is present in the App output directory.
- Check that the native build directory is `native/out/build/release`; update the path in `Weftline.Native.csproj` if you use another directory.

### Windows DLL load failure

- Ensure the native core and the .NET process use the same architecture (usually x64).
- Rebuild the native core from “x64 Native Tools Command Prompt for VS 2022”.

### Linux font/rendering issues

- Install a CJK font (recommended: `Noto Sans CJK SC`).
- Try a Release build.
