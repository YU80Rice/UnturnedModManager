# 🚀 Unturned Mod Manager

> 一站式极速启动、多核性能优化与局域网 / P2P 穿透联机大厅管理器。
> 为《未转变者》（Unturned）玩家社区打造的开放性底层逻辑引导与开发平台。

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF-UI](https://img.shields.io/badge/WPF--UI-3.0.5-CA1E1E?logo=wpf)](https://github.com/lepoco/wpfui)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![Release](https://img.shields.io/badge/Release-v1.6.1-brightgreen?logo=github)](https://github.com/YU80Rice/UnturnedModManager/releases)
[![BepInEx](https://img.shields.io/badge/BepInEx-5-FF7B00?logo=nuget)](https://github.com/BepInEx/BepInEx)

🌐 **开源仓库**：[github.com/YU80Rice/UnturnedModManager](https://github.com/YU80Rice/UnturnedModManager)

---

## 📖 项目简介

`Unturned Mod Manager`（简称 UMM）是一款基于 .NET 8 + WPF-UI 构建的《未转变者》启动器与模组管理前端。

它并非另一个创意工坊——创意工坊决定了"玩什么"，而 UMM 决定了"**怎么玩**"。我们提供一套 **WPF 引导前端 + 独立 BepInEx 插件** 架构，打破官方启动器的技术壁垒，让玩家能一键自主为游戏重塑更丝滑的筋骨。

---

## ✨ 核心特性

### 🎨 Fluent 设计 · Mica 亚克力背景
基于 [WPF-UI 3.0.5](https://github.com/lepoco/wpfui) 构建，深度还原 Windows 11 Fluent Design 设计语言：
- **Mica 半透明背景**：自动跟随系统壁纸色调，融入桌面氛围
- **深色 / 浅色主题一键切换**：状态持久化到 `config.json`
- **NavigationView 侧边栏导航**：圆角卡片、InfoBar、SymbolIcon 全套 Fluent 控件

### 🚀 双轨启动 · 智能避开 BattlEye 反作弊
- **模组模式**：直接启动 `Unturned.exe -NoBattlEye`，绕过 BE + 加载 BepInEx 模组链
- **原版模式**：直接启动 `Unturned_BE.exe`（无参数），原生 BE + Steam 覆盖层
- 全局开关切换，零侵入游戏可执行文件

### 📦 一键环境部署 · 自愈式修复
- **BepInEx 5 自动下载**：GHProxy 镜像加速 + GitHub 官方 fallback 双轨
- **DXVK 2.4 流式解压**：GZipStream + TarReader 仅提取 x64 必要文件
- **`.disabled` 重命名模式**：关闭时重命名为 `.disabled`，开启时从 `.disabled` 恢复，避免反复下载
- **winhttp.dll 双轨切换**：BepInEx 注入层一键启用 / 停用

### 🧩 嵌入式核心模组自释放
启动器自带两款核心优化模组作为内嵌资源（`<Resource>` 项嵌入到 `.g.resources`）：
- `LaunchPerfOptimizer_v1.0.dll` — 启动哈希缓存与加载时间超频优化
- `WaterPerfOptimizer_v1.0.dll` — 水体物理查询与限帧优化

BepInEx 解压完成后自动调用 `DeployEmbeddedCoreMods` 释放到 `Unturned/BepInEx/plugins/`。**落地名去版本后缀**，避免 BepInEx 重复加载版本化与未版本化副本。

### 🔧 DXVK 2.4 转译加速 · Vulkan 后端
- **DX11 → Vulkan 转译**：95% 兼容率（Unity 2022.3.62f3 LTS + Cg/HLSL 着色器）
- **动态 `dxvk.conf`**：根据 `Environment.ProcessorCount` 写入 `dxvk.numCompilerThreads = N-1`
- **管线库加速**：`dxvk.enableGraphicsPipelineLibrary = True` 减少着色器卡顿
- **DXVK_HUD 注入**：`compiler,fps,api` 实时调试 HUD（仅启用 DXVK 时注入环境变量）

### 🌐 Steam 路径智能探测
- **注册表双查**：`HKEY_CURRENT_USER\Software\Valve\Steam` + `HKEY_LOCAL_MACHINE\...Steam App 304930`
- **libraryfolders.vdf 遍历**：支持多 Steam Library 自定义安装路径
- 自动校验 Unturned 安装完整性，路径用于 BepInEx / DXVK / winhttp.dll 部署

---

## 📥 下载与使用

### 方式一：直接下载 Release（推荐普通玩家）

1. 前往 [Releases 页面](https://github.com/YU80Rice/UnturnedModManager/releases)
2. 下载最新版 `UnturnedModManager.exe`（约 71 MB，单文件自包含，**无需安装 .NET 运行时**）
3. 双击运行即可

### 方式二：从源码构建（开发者）

#### 环境要求
- Windows 10 / 11 x64
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 / VS Code / Rider（可选）

#### 构建步骤
```bash
git clone https://github.com/YU80Rice/UnturnedModManager.git
cd UnturnedModManager
dotnet build -c Release
```

#### 单文件发布（生成 Release 资产）
```bash
dotnet publish -c Release -r win-x64 \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --self-contained true
```
产物位于 `bin/Release/net8.0-windows/win-x64/publish/UnturnedModManager.exe`

### 运行环境
- Windows 10 / 11 x64
- Steam 已安装 Unturned（App ID 304930）

---

## 🛠️ 技术栈

| 层级 | 技术 |
|---|---|
| 前端框架 | .NET 8.0 WPF (`net8.0-windows`) |
| UI 库 | [WPF-UI 3.0.5](https://github.com/lepoco/wpfui)（Mica + Fluent 控件）|
| 模组加载器 | [BepInEx 5](https://github.com/BepInEx/BepInEx)（Harmony 2 补丁框架）|
| 图形转译 | [DXVK 2.4](https://github.com/doitsujin/dxvk)（DX11 → Vulkan）|
| 通信库 | SDG.Steamworks（P2P 联机底层）|

---

## 📂 项目结构

```
UnturnedModManager/
├── App.xaml / App.xaml.cs           # WPF 应用入口
├── AppSettings.cs                   # 持久化设置（ThemeMode、SteamPath 等）
├── MainWindow.xaml / .cs            # 主窗口（FluentWindow + NavigationView）
├── UnturnedModManager.csproj        # 项目文件（.NET 8 + WPF-UI 3.0.5）
├── Pages/
│   ├── HomePage.xaml / .cs          # 游戏启动页（双轨启动 + BepInEx/DXVK 部署）
│   ├── ModListPage.xaml / .cs       # 模组管理页
│   ├── SettingsPage.xaml / .cs      # 设置页
│   └── AboutPage.xaml / .cs         # 关于页（含极客宣言）
├── Resources/
│   ├── LaunchPerfOptimizer_v1.0.dll  # 嵌入式核心模组（启动优化）
│   └── WaterPerfOptimizer_v1.0.dll   # 嵌入式核心模组（水体优化）
├── Helpers/
│   └── WpfUiDialogHelper.cs         # WPF-UI 对话框辅助
├── Models/
│   └── ModItem.cs                   # 模组数据模型
├── Services/                        # 业务服务层
├── Views/                           # 视图
├── Assets/                          # 图标与图片资源
├── Properties/                      # WPF 属性元数据
├── LICENSE                          # MIT 许可协议
└── README.md                        # 本文档
```

---

## ⚖️ 极客宣言：关于 Vibecoding 与开源授权

### 🤝 Vibecoding 真诚声明

本项目完全采用 **Vibecoding 范式** 开发——一种人类与硅基智能深度协作的全新编程范式。我们坦诚相告这部作品的诞生路径：

| 角色 | 职责 | 工具 / 实体 |
|---|---|---|
| 🧑‍🎤 **人类导演** | 提出愿景、把控方向、设计架构哲学、把关产品审美 | **YU80Rice**（项目发起人）|
| 🧠 **硅基顾问** | 提供高阶架构蓝图、算法设计、深层 Bug 诊断、Unity / Steamworks 生命周期解构 | **Claude / Kimi**（AI 导师）|
| 🐾 **樱爪 Agent** | 本地代码重构、编译、在 U3-SDK 上万行源码中扫描类结构、解决编译警告 | **Cherry Claw**（CherryStudio 本地 Agent）|

每一行代码、每一处 UI、每一项功能，都凝聚了人类直觉与硅基逻辑的深度协作。我们不以"全自研"为荣，而以"开放协作"为傲。**知识应当如流水般自由流动，而非被锁在商业牢笼中。**

这不是一段黑箱，而是一场坦诚的人机协同实验。我们公开这场协作的每一处脉络——从最初的产品构想、架构推演、算法选择，到最终的代码实现与编译验证——全部接受社区检阅。

### 📜 MIT 开源许可

本项目基于 [**MIT 协议**](./LICENSE) 开源。

```
MIT License

Copyright (c) 2026 YU80Rice
```

这意味着您拥有自由使用、修改、分发、商业利用的权利，**唯一的要求**是在所有副本或实质性部分中保留版权声明与许可声明。我们坚信：开源不是施舍，而是一种**让技术社区持续进步的真正动力**。

### 🕊️ 致意与免责

《未转变者》（Unturned）的全部版权归 **Smartly Dressed Games** 所有。本启动器仅为玩家社区的非官方辅助工具：

- ❌ 不包含任何游戏资产
- ❌ 不修改游戏可执行文件
- ✅ 所有 BepInEx 插件均以 `.dll` 独立文件形式部署，可随时通过 `.disabled` 后缀停用或物理删除

我们致敬 Nelson 的开源精神，并承诺本项目**永远免费、永远开源**。

---

## 🙏 致谢

- [Smartly Dressed Games](https://smartlydressedgames.com/) — Unturned 原作开发商，开源 U3-SDK
- [BepInEx Team](https://github.com/BepInEx) — 强大的 .NET 模组加载器
- [doitsujin](https://github.com/doitsujin) — DXVK Vulkan 转译层
- [lepoco](https://github.com/lepoco) — WPF-UI Fluent 控件库
- [Anthropic](https://anthropic.com/) — Claude AI 顾问
- [Moonshot AI](https://www.moonshot.cn/) — Kimi AI 顾问
- [CherryStudio](https://github.com/CherryHQ/cherry-studio) — 本地 Agent 工作台

---

## 📬 联系与反馈

- 🐛 **Bug 反馈**：[Issues](https://github.com/YU80Rice/UnturnedModManager/issues)
- 💡 **功能建议**：[Discussions](https://github.com/YU80Rice/UnturnedModManager/discussions)
- 🔄 **最新版本**：[Releases](https://github.com/YU80Rice/UnturnedModManager/releases)

---

<details>
<summary>📖 展开查看：项目初心与创意工坊的区别</summary>

我们并不是要挑战创意工坊，我们极度热爱它。创意工坊是内容分发网络（UGC），它决定了玩家"**玩到什么地图、用什么炫酷的枪械和道具**"。

本启动器是一个"**开放性的、属于玩家自己的底层逻辑引导与开发平台**"。在创意工坊上，玩家无法直接一键部署涉及游戏底层重构的客户端脚本模组（如自动理包、P2P 好友直连、多核优化）。

我们提供这套"WPF 引导前端 + 独立 BepInEx 插件"的架构，就是为了打破技术壁垒。创意工坊赋予了游戏丰富的血肉，而我们希望通过这个平台，让玩家能一键自主为游戏重塑更丝滑的筋骨。

</details>

---

<p align="center">
  <sub>Built with ❤️ by <a href="https://github.com/YU80Rice">YU80Rice</a> & Vibecoding Collaborators</sub><br>
  <sub>© 2026 Unturned Mod Manager · MIT License</sub>
</p>
