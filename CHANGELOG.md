# 📒 更新日志 (Changelog)

本文件记录 UnturnedModManager 启动器的所有版本变更。
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)：主版本.次版本.修订号。

---

## [v2.0.0] - 2026/08/14

v2.0 是 UMM 的一次大版本重构。项目继续采用 .NET 8 + WPF，但界面状态、导航、社区访问、本地插件管理和安装行为已从页面事件代码中拆分为可维护的 ViewModel/Service 架构。

### 🎨 界面与交互
- 重构主窗口、游戏启动、本地插件、社区、详情、设置和账户界面，统一加载、空数据、错误及操作反馈。
- 社区首页改为填满内容区域的条目列表；点击条目后进入独立详情页。
- 新增带来源上下文的导航历史：从本地插件跳转到社区详情后，“返回”会回到本地插件页。
- 修复卡片区域滚轮事件被背景控件截获的问题。
- 修复首次启动时侧栏折叠状态、账户入口和主题按钮形态不同步的问题。
- 持久化侧栏、窗口大小、位置、最大化状态以及“浅色/深色/跟随系统”主题。
- 侧栏新增社区账户入口；展开时显示头像与昵称，折叠时显示头像。

### 🌐 unmod.online 社区
- 新增社区列表、缩略图、分类、排序、防抖搜索与自动刷新。
- 新增社区详情页、依赖展示、安装、更新和卸载操作。
- 使用系统浏览器完成登录与人机验证，通过本机 `localhost` 回调接收令牌。
- 保存账户令牌、昵称和头像，并区分“服务器已验证会话”与“仅有本地缓存账户”。
- 新增分类、列表和详情元数据缓存；网络不可用时回退最近缓存。
- 修复本地化文本只能读取、不能写入 JSON 的问题。

### 🧩 本地插件与安装安全
- 将自动安装、升级和修复基线更新为官方 BepInEx 5.4.23.5 win_x64，并增加官方发布包 SHA-256 校验。
- 新增 BepInEx 多源下载回退：已登录时优先使用 unmod.online 社区包，未登录时跳过社区源并依次尝试 `gh-proxy.com`、`ghproxy.net` 国内镜像，最后回退 GitHub 官方；所有来源统一执行 SHA-256 校验。
- 新增“卸载环境”：移除 BepInEx 核心与 winhttp doorstop 文件，同时默认保留 plugins、config、cache、日志和社区安装记录。
- 本地插件支持启用、停用、导入和卸载，兼容玩家手动安装的 DLL。
- 本地条目显示社区标题与 DLL 文件名，并可匹配、跳转和更新对应社区版本。
- 社区安装器新增依赖递归、循环依赖检测、文件所有权清单、冲突阻止、更新回滚和卸载哈希校验。
- 阻止 ZIP 路径穿越及社区包覆盖核心游戏文件，并限制条目数量和解压总体积。
- 安装状态与游戏目录进行协调，避免文件已移除后社区仍显示“已安装”。

### 🏗️ 架构与发布
- 页面后台代码收敛为视图职责，业务逻辑迁移至 ViewModels、Services 和 Models。
- 目标框架统一为稳定版 `net8.0-windows`，正式发布不依赖 .NET 10 Preview。
- 取消安装、修复或启动 BepInEx 时强制部署 `LaunchPerfOptimizer` 与 `WaterPerfOptimizer`。
- 将 `UnturnedModManager.Tests` 正式纳入仓库，覆盖本地化、版本比较和社区缓存行为，并新增 Windows GitHub Actions 构建测试工作流。
- 将程序集、文件和包版本统一升级为 `2.0.0`。

### 📜 许可证与致谢
- 新版本许可证由 MIT 改为 `GPL-2.0-only`。分发 GPL 衍生版本时须继续采用 GPL v2 并提供相应源代码。
- 在 README 和关于页明确致谢 [Ayndpa/unturned-mod-loader](https://github.com/Ayndpa/unturned-mod-loader) 对信息架构与社区交互的启发。
- 将 UML 作者 [Ayndpa](https://github.com/Ayndpa) 列为贡献者，并记录其通过 UMM PR #7 提交的滚轮交互修复。
- 补充 OpenAI GPT（Codex）及其他 AI 协作者在 v2.0 开发中的具体职责。

### 🧪 验证
- Debug 与 Release 构建：0 警告、0 错误。
- 自动化测试：7/7 通过，包含 BepInEx 核心卸载时保留玩家数据、未登录跳过社区源及社区 Cookie 发送的回归测试。
- `win-x64` 自包含单文件发布验证通过，发布产物不再包含两款待整合优化插件。

---

## [v1.6.8] - 2026/07/15

- 新增 GPU 架构检测，根据兼容性启用或关闭 DXVK 默认建议。
- 对兼容性一般的显卡显示 DXVK 风险提示。
- 合并 Ayndpa 提交的滚轮交互修复（PR #7）。

## [v1.6.7] - 2026/07/14

- 移除 DXVK HUD 环境变量注入，避免游戏左上角出现调试信息。

## [v1.6.6] - 2026/07/13

- 修复 Steam Overlay 无法呼出和 DXVK 偶发闪退问题。

## [v1.6.5] - 2026/07/12

- 修复旧版 BepInEx 环境缺少当时内嵌核心模组时的启动异常。该强制部署机制已在 v2.0 移除。

## [v1.6.4] - 2026/07/12

- 将首页已验证的滚轮路由模式应用到关于页。

## [v1.6.3] - 2026/07/12

- 改用 `PreviewMouseWheel` 隧道事件修复卡片区域滚动。

## [v1.6.2] - 2026/07/12

### 🐛 Bug 修复
- **修复首页（HomePage）卡片区域滚轮失效**：BepInEx 状态、全局模组开关、DXVK 优化卡片在鼠标悬停时无法响应滚轮事件，现已通过 `PreviewMouseWheel` 隧道事件 + `VisualTreeHelper` 向上递归寻找最近 `ScrollViewer` 祖先的方式手动路由滚轮偏移。
- **修复设置页（SettingsPage）卡片区域滚轮失效**：应用同样的滚轮穿透修复，覆盖游戏路径选择卡片。

### 🔧 技术细节
- 在 `Pages/HomePage.xaml` 与 `Pages/SettingsPage.xaml` 的最外层卡片包裹容器上订阅 `PreviewMouseWheel="CardPanel_PreviewMouseWheel"`。
- 在对应 `.xaml.cs` 中实现通用 `CardPanel_PreviewMouseWheel` 回调：
  - 通过 `VisualTreeHelper.GetParent` 向上递归寻找 `ScrollViewer` 祖先。
  - 命中后调用 `ScrollToVerticalOffset(VerticalOffset - e.Delta)` 手动应用滚动偏移。
  - 设置 `e.Handled = true` 完成事件截断，防止二次干扰。
- 此方案沿用 v1.6.1 关于页（AboutPage）的修复模式，但适配了"无显式 ScrollViewer 命名"的场景，复用 NavigationView 内部 ScrollViewer。

### 📦 构建产物
- 文件名：`UnturnedModManager_v1.6.2.exe`
- 体积：~70.4 MB（与 v1.6.0 基线一致，启用 `EnableCompressionInSingleFile` + `DebugType=embedded`）
- 类型：自包含单文件（self-contained single-file），无需用户安装 .NET 8 运行时
- 目标架构：win-x64

---

## [v1.6.1] - 2026/07/11

### 🐛 Bug 修复
- 修复关于页（AboutPage）滚轮失效：通过显式 `ScrollViewer` 命名 + `PreviewMouseWheel` 手动滚动。
- 添加 GitHub 仓库链接至关于页。

### 📜 协议同步
- 关于页 GPL-2.0 -> MIT 协议同步。

---

## [v1.6.0] - 2026/07/11

### 🚀 新功能
- **嵌入式核心模组自释放**：启动器自带 `WaterPerfOptimizer_v1.0.dll` 与 `LaunchPerfOptimizer_v1.0.dll`，BepInEx 解压完成后自动释放到 `BepInEx/plugins/`，落地名去版本后缀以避免 BepInEx 重复加载。
- **DXVK 2.4 转译部署**：从 tar.gz 流式提取 `x64/d3d11.dll` + `x64/dxgi.dll`，支持 `.disabled` 后缀切换模式。
- **winhttp.dll 双轨切换**：模组模式 / 原版模式无缝切换，无需重新部署。
- **动态 dxvk.conf**：根据 CPU 物理线程数自适应 `numCompilerThreads`，开启 `enableGraphicsPipelineLibrary` 减少着色器卡顿。
- **DXVK_HUD 注入**：开启 DXVK 时自动注入 `compiler,fps,api` HUD。
- **Steam 路径双轨探测**：注册表 + `libraryfolders.vdf` 遍历。

### 🎨 UI
- 升级至 WPF-UI 3.0.5（Mica 背景 + NavigationView + InfoBar）。
- 主题切换：`Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica)`。
- 深色 / 浅色主题持久化至 `AppSettings.ThemeMode`。

---

## [v1.4.0] - 2026/07/08

### 🎉 首次开源发布
- 项目首次开源至 GitHub。
- 实现基础启动器、BepInEx 部署、Steam 路径探测、主题切换。
