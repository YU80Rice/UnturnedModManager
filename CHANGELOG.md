# 📒 更新日志 (Changelog)

本文件记录 UnturnedModManager 启动器的所有版本变更。
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)：主版本.次版本.修订号。

---

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
