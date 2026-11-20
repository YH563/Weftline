# 后续开发路线图

本文整理 Weftline 在“剧情预览可用、桌面编辑器已接入、入口与 `.wstory` 生成已打通”之后的后续方向。

当前基线：

- HTML 导出尚未实现；`RuntimeProjection` 已提供单文件播放所需的剧情、分支、变量和图片路径。
- 项目级持久化目前只有 `weftline.layout.json` 布局侧车；没有应用设置、最近项目、窗口/面板状态和会话恢复。
- 前端已具备右键设入口、生成项目文件、编译图标、入口补全和 Problems 空状态；Output/ProjectInfo 面板仍未接入。
- 发布仍以本地 `dotnet publish` 和手动复制 native 动态库为主，没有 RID 构建、安装包和 CI。

---

## 1. HTML 后端导出与预览

目标：从当前项目生成可离线双击打开的单文件 HTML，播放行为与原生预览一致。

范围：

- C++ core 新增导出模块，消费 `RuntimeProjection`。
- 单文件 HTML：内嵌 runtime JSON、轻量 JS 播放器、CSS 和图片 data URI。
- 支持正文推进、选项、条件、布尔变量、`@set` 效果、图片和 END。
- 不包含 `.wstory` 设计记录、源码位置、诊断或编辑器分析数据。

图片资源的关键约定：

- core 不访问磁盘。
- 建议新增最小资源接口，由宿主读取图片后传入：
  - `weft_project_set_asset(project, path, bytes, length)`
  - `weft_project_export_html(project, flags) -> char*`（由 `weft_free` 释放）
- 备选方案是导出时写相对路径、由 C# 后处理嵌入，但会把导出逻辑拆成两半，不优先采用。

交付：

- `native/weftline-core/export/`：HTML writer、JS/CSS 模板和资源嵌入。
- `weftc export`：命令行导出，便于 CI 和批量生成。
- C ABI 与 `interop/contract` 同步更新。
- 前端“运行 → 导出 HTML…”：选择输出路径，成功后可直接打开浏览器预览。
- 测试：样例 golden HTML；条件、选项、图片、转义和路径安全；与 `PlayerSession` 行为对照。

验收：

- 断网环境打开导出 HTML，可以完整走完样例剧情。
- 图片不丢失，`</script>`、引号和中文路径正确转义。
- 1MB 项目导出时间可接受。

---

## 2. 项目与应用持久化

原则：

- `.weft` / `.wstory` 是源码真相，不把 UI 状态写入 `.wstory`。
- 项目级 UI 状态继续放在 `weftline.layout.json` 这类侧车。
- 应用级状态单独放在 `settings.json`。

应用级持久化：

- 最近项目列表（建议最多 10 个，失效路径可清理）。
- 上次打开的项目，以及启动时是否自动恢复。
- 窗口位置、大小和最大化状态；多显示器下校验可见性。
- 面板宽度、预览/剧情图/Problems 可见状态。
- 上次打开的文件标签，可选恢复光标和滚动位置。
- 编辑器字号、预览起点偏好。
- 原子写入、`schemaVersion`、损坏时回退默认值。
- 平台路径：Linux `~/.config/Weftline`，Windows `%APPDATA%\Weftline`，macOS `Application Support`。

项目级持久化：

- 保持现有 `weftline.layout.json`，补版本迁移和损坏容错。
- 可选记录上次选中章节/模块、预览起点和筛选条件。
- 未保存恢复：自动保存临时快照，启动时提示恢复。

交付：

- `AppSettingsStore`、`RecentProjectsService`、`SessionStore`。
- 启动恢复、最近项目菜单、窗口和面板状态保存。
- 测试：损坏 JSON、缺失项目、窗口越界和 schema 迁移。

验收：

- 关闭再打开应用，恢复到上次项目和主要布局。
- 配置文件损坏时不崩溃，给出可恢复的默认状态。

---

## 3. 前端操作丰富

优先补：

- Output / ProjectInfo 面板：日志、分析状态、项目名、章节、符号和入口。
- 真正的键盘快捷键：`Ctrl+N/O/Z/Y`、`Ctrl+Enter`、`Ctrl+Shift+B`、导出 HTML、生成项目文件。
- 项目资源管理器右键菜单：新建、重命名、删除、生成项目文件、在系统文件管理器中打开。
- 剧情图右键菜单扩展：设为入口、从该模块预览、复制模块 ID、跳转源码。
- HTML 导出 UI：路径选择、进度、成功/失败状态、打开导出目录或浏览器。
- 编辑器：查找/替换、跳转定义、关闭其他/全部标签。
- 预览面板：长列表虚拟化；保留开始模块选择。
- 设置对话框：最近项目、编辑器字号、默认导出目录和面板行为。

可延后：命令面板、可自定义快捷键、多窗口和插件系统。

---

## 4. 软件打包与发布

构建：

- RID 矩阵：`linux-x64`、`win-x64`、`osx-arm64`，后续补 `osx-x64`。
- 每个 RID 先构建对应 native core，再 `dotnet publish --self-contained`。
- 改造 `Weftline.Native.csproj`：按配置/RID 查找并复制动态库，不再固定 `native/out/build/release`。
- 版本号统一：`weft_version`、AssemblyVersion/FileVersion 和关于对话框一致。

安装包：

- Linux：`tar.gz` + `.desktop` + 图标，后续 AppImage/deb。
- Windows：MSIX 或 Inno Setup；支持 `.weft` / `.wstory` 文件关联。
- macOS：`.app` + DMG；签名与 notarization 需要证书，先预留流程。
- 启动冒烟：用 `preview.sh --shot` 或 headless 启动检查 native 库加载和首屏渲染。

CI：

- GitHub Actions 矩阵：native build + ctest、dotnet build/test、publish、安装包和 artifact 上传。
- 缓存 NuGet/CMake；失败时保留日志和截图。
- 发布说明带版本号和变更摘要。

验收：

- 三个平台安装后能启动、打开样例项目并导出 HTML。
- 动态库随包分发，不依赖开发目录。
- 安装/卸载不残留异常文件。

---

## 推荐顺序

| 阶段 | 内容 | 依赖 |
|---|---|---|
| M9 | HTML 导出 MVP + CLI + 前端导出入口 | 先定资源 ABI |
| M10 | 应用设置、最近项目、窗口与面板持久化 | 独立，可并行 |
| M11 | Output/ProjectInfo、真实快捷键、右键菜单、导出 UI | M9 |
| M12 | RID 打包、CI、安装包和签名 | M9/M10/M11 稳定后 |

建议先做 M9 的资源 ABI 和 M10 的基础设置存储；它们会同时服务导出、最近项目和打包。前端操作丰富可以穿插进行，打包放在最后，避免反复调整安装包。

---

## 风险与决策点

- HTML 图片嵌入需要新增 C ABI；要在“core 不访问磁盘”和“单文件导出”之间保持边界清晰。
- macOS 签名/notarization、Windows 签名需要证书，可能影响发布时间。
- self-contained 发布体积较大，是否启用 trimming/AOT 需要单独验证 Avalonia 和 AvaloniaEdit 的兼容性。
- 持久化要区分应用级设置、项目级侧车和源码，避免把 UI 状态写进 `.wstory`。
- 前端操作容易扩散，建议按“导出 HTML、最近项目、快捷键、面板”四项分批交付。
