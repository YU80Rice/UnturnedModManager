# Unturned Mod Manager v2.1.1

> 面向 Windows 的 Unturned 启动、BepInEx 插件管理与 [unmod.online](https://unmod.online/) 社区客户端。

[![License: GPL v2](https://img.shields.io/badge/License-GPL--2.0--only-blue.svg)](./LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF-UI](https://img.shields.io/badge/WPF--UI-3.0.5-CA1E1E)](https://github.com/lepoco/wpfui)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![Release](https://img.shields.io/badge/Release-v2.1.1-brightgreen?logo=github)](https://github.com/YU80Rice/UnturnedModManager/releases)
[![Build and test](https://github.com/YU80Rice/UnturnedModManager/actions/workflows/ci.yml/badge.svg)](https://github.com/YU80Rice/UnturnedModManager/actions/workflows/ci.yml)

**仓库：** [github.com/YU80Rice/UnturnedModManager](https://github.com/YU80Rice/UnturnedModManager)

UMM 是非官方社区项目，不隶属于 Smartly Dressed Games、Valve、BepInEx 或 unmod.online。项目不包含 Unturned 游戏资产，也不会修改游戏可执行文件。

---

## 项目初心：创意工坊赋予血肉，UMM 重塑筋骨

我们并不是要挑战 Steam 创意工坊——恰恰相反，我们极度热爱它。创意工坊让玩家获得地图、武器、载具和无数由社区创造的内容，它为 Unturned 提供了丰富而鲜活的“血肉”。

但仍有一类改变很难仅靠创意工坊完成：客户端逻辑插件、加载器环境、底层体验改进、插件依赖与版本管理，以及玩家对自己本地游戏环境的掌控。UMM 最初就是为了填补这块空白而诞生。

我们希望它成为一个**开放的、属于玩家自己的底层逻辑引导与开发平台**：把原本需要查阅教程、手动复制 DLL、修改文件名和排查依赖的过程，变成看得见、可理解、可撤销的操作。创意工坊决定玩家“玩到什么”，UMM 希望帮助玩家更自由地决定“怎么玩”。

“WPF 启动与管理前端 + 独立 BepInEx 插件”并不是为了炫耀技术栈，而是为了降低使用和开发门槛：让愿意折腾的人仍然拥有控制权，也让不熟悉目录结构和加载链的玩家能够安全地迈出第一步。

这个项目同样承载着人与朋友们在 PEI 篝火旁度过的记忆。它不是冷冰冰的功能集合，而是一封写给 Unturned 社区、开源协作者和仍然热爱这款游戏的玩家们的信。

---

## 设计来源与特别致谢

UMM v2.0 在信息架构、社区浏览流程、列表—详情交互、浏览器登录回调和导航完整性方面，明确借鉴并学习了 [Ayndpa/unturned-mod-loader](https://github.com/Ayndpa/unturned-mod-loader)（下文简称 UML）的设计经验。

感谢 [@Ayndpa](https://github.com/Ayndpa)：

- 创建并开放 UML，为 Unturned 启动器与社区插件管理提供了可参考的完整实践；
- 通过 [UMM Pull Request #7](https://github.com/YU80Rice/UnturnedModManager/pull/7) 直接参与 UMM，贡献滚轮交互修复；
- 其工作帮助 UMM v2.0 重新审视社区页、详情页、账户入口和导航返回逻辑。

UMM v2.0 不是 UML 的官方分支或继任版本。UMM 保留 .NET 8 + WPF 技术路线，并针对自身代码、数据模型和安装安全策略进行了独立实现。使用或研究 UML 时，请同时遵守其仓库当前声明的许可证。

项目交互也受到 [PCL2](https://github.com/Hex-Dragon/PCL2) 等成熟启动器的启发；这里的致谢表示设计学习，不代表这些项目对 UMM 的认可或背书。

---

## v2.1.1 能做什么

### 游戏与插件环境

- 自动探测 Steam Library 中的 Unturned，亦可手动选择游戏目录；
- 下载、安装、升级、修复或卸载社区统一基线 BepInEx 5.4.23.5（win_x64，Unity Mono / winhttp doorstop）；
- 通过 `winhttp.dll` / `winhttp.dll.disabled` 切换 BepInEx 注入状态；
- 模组模式使用 `Unturned.exe -NoBattlEye`，原版模式使用 `Unturned_BE.exe`；
- 可选部署 DXVK 2.4，并根据检测到的 GPU 架构给出兼容性提示。
- 可在首页分析本机 Unity、Unturned、BepInEx 与 DXVK 日志，导出不上传的诊断包以协助排查异常退出。

配置默认保存于 `%AppData%\Roaming\UnturnedModManager\config.json`。发布包目录不保存用户配置；如需隔离验收或便携式调试，可在启动前设置 `UMM_DATA_DIRECTORY`，配置和社区缓存会一起写入该目录。

> DXVK 的效果取决于显卡、驱动和游戏环境。UMM 会跳过远程/投屏虚拟显示驱动，优先分析实际物理显卡；即使显卡具备 Vulkan 支持，Windows 原生 D3D11 也不一定更慢，因此应以相同场景的帧率和稳定性实测为准。

BepInEx 安装包使用多源回退策略，并在解压前统一校验 SHA-256：

1. 已登录时优先使用 unmod.online 社区包（社区条目 ID `4`，需要社区账户 Cookie）；
2. 未登录时自动跳过社区源，然后尝试国内镜像 `gh-proxy.com` 与 `ghproxy.net`；
3. 镜像不可用时回退到 GitHub 官方发布地址。

因此，国内镜像仍然保留，但镜像服务属于公共代理，可能因网络、限流或服务维护而暂时不可用。启动器会在状态提示中显示当前尝试的源，不会因为镜像失败而阻塞安装。无论下载自社区、镜像还是 GitHub，均必须通过同一份 BepInEx 5.4.23.5 包校验。

UMM 不再内嵌、安装或启动时覆盖 `LaunchPerfOptimizer` 与 `WaterPerfOptimizer`。两个文件仅暂存为后续整合原材料，不进入发布产物。

“关闭插件环境”只会停用 `winhttp.dll`，适合临时以原版模式运行；“卸载环境”会移除 BepInEx 核心和 Doorstop 启动文件，但保留 `plugins`、`config`、缓存、日志与社区安装记录，方便以后重新安装后继续使用。

### 本地插件管理

- 递归扫描 `BepInEx/plugins` 下的 `.dll` 与 `.dll.disabled`；
- 启用、停用、导入和卸载本地插件；
- 区分社区托管插件与玩家手动安装插件；
- 将本地 DLL 名称、程序集信息与社区条目进行匹配；
- 从本地插件跳转到对应社区详情，并支持社区版本更新；
- 保留来源页导航上下文，详情页“返回”会回到真正的上一级页面。
- 支持为每个 Unturned 安装目录保存多个“插件方案”：方案记录全部本地插件的启停状态，例如“联机优化（ABC）”或“开发调试（XYZ）”；应用方案时未包含的现有插件会被停用，但 DLL、社区安装记录与 BepInEx 配置都不会被删除或复制。

### unmod.online 社区

- 浏览器登录并通过本机 `localhost` 回调接收社区令牌；人机验证始终由用户在网页中完成；
- 社区列表、缩略图、分类、排序和防抖搜索；筛选条件变化后自动刷新；
- 列表内查看摘要，进入独立详情页后查看完整信息和执行安装；详情支持基础 Markdown 排版、依赖条目跳转及来源上下文返回；
- 插件封面可点击进入独立图片预览，支持滚动、缩放、Ctrl + 滚轮和 Esc 关闭；详情正文中的 HTTPS Markdown 图片也会以缩略图库形式提供同样的安全预览；
- 详情页展示作者、版本、分类、文件大小、下载量、点赞量、依赖数量和当前安装状态，不以伪造数据补充社区未提供的图库；
- 安装依赖、更新、卸载及安装清单同步；
- 新增任务中心：社区插件安装、更新与卸载均可显示流式下载百分比、已下载大小、当前阶段、失败原因、尝试次数及操作历史；失败任务可在同一次启动中重试；
- 分类、列表和详情元数据缓存；网络暂时不可用时可读取最近缓存；
- 已验证会话与“仅有本地缓存账户”状态分离，避免离线时误启用受保护操作。

### 安装安全与可恢复性

- 拒绝 ZIP 路径穿越和对游戏核心文件的直接覆盖；
- 限制压缩包条目数量及解压后总体积；
- 记录社区插件文件所有权，阻止不同插件静默覆盖同一路径；
- 更新失败时恢复旧文件；卸载前校验文件哈希，发现用户修改后停止删除；
- 社区安装清单与备份位于 `%AppData%\UnturnedModManager\community-mods`。

这些保护降低了误操作风险，但不等同于完整沙箱。安装第三方插件前仍应确认来源并备份重要数据。

---

## 界面与状态持久化

- WPF-UI 3.0.5 Fluent 控件与深色、浅色、跟随系统三种主题；
- 侧边栏展开状态、窗口尺寸和位置持久化；
- 折叠侧栏显示账户头像和完整主题图标；
- 社区账户昵称、头像与登录令牌在本地保存，以便下次启动恢复会话；
- 页面加载、空状态、错误状态和缓存状态使用独立反馈。
- 设置、列表和详情页会将滚轮交给鼠标下方最近的可滚动区域；本地插件条目可用 Enter 键进入其社区详情。
- 首次启动提供三步引导：游戏目录、主题偏好和功能说明；已经配置过游戏目录的旧版本升级不会被重复打断。
- 启动器启用单实例保护：第二次启动会唤醒已有窗口，并将安装意图安全转交给它，避免多个窗口同时修改同一套插件文件。
- 注册独立的 `umm://install/{社区插件 ID}` 协议入口；它只打开对应插件详情供用户确认安装，不会抢占 UML 使用的 `unmod://` 协议。
- 提供六套可持久化配色；非默认配色会同时更新按钮、开关、导航选中、进度条和焦点等交互状态，而不只是改变页面背景。
- 首页提供可在设置中关闭的 Q 版吉祥物欢迎区与版本公告；每次升级后会重新展示对应摘要，只在本地识别程序版本，不会后台下载或静默安装更新。

社区令牌当前保存在 `%AppData%\UnturnedModManager\config.json`；插件方案保存在同一数据根目录的 `plugin-profiles` 下，任务历史保存在 `task-history.json`，并按游戏安装目录隔离。请将该目录视为敏感数据，不要公开上传或附在 Issue 中。

---

## 技术架构

| 层级 | 实现 |
|---|---|
| 运行时 | .NET 8，`net8.0-windows` |
| 桌面 UI | WPF + [WPF-UI 3.0.5](https://github.com/lepoco/wpfui) |
| 表现层 | Pages + ViewModels，命令、状态和导航上下文分离 |
| 业务层 | BepInEx、DXVK、本地插件、社区 API、安装器、账户与缓存服务 |
| 操作中心 | 独立任务服务保存进度、失败原因与有限历史；重试委托只在当前启动会话有效 |
| 社区 | [unmod.online](https://unmod.online/) HTTP API |
| 插件环境 | [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5)，win_x64 / Unity Mono / winhttp doorstop |
| 图形转译 | [DXVK 2.4](https://github.com/doitsujin/dxvk)（可选） |

v2.0 继续使用 WPF，因为 UMM 的目标平台、现有组件和 Unturned 运行环境均以 Windows 为中心。此次升级重点是建立清晰的 ViewModel/Service 边界、可靠状态模型和可测试业务逻辑，而不是仅为更换技术名称迁移 UI 框架。

UMM 明确不采用 UML 的 WinFsp 虚拟文件系统、虚拟盘挂载或挂载式多 profile。Unity 对虚拟文件系统读取存在兼容风险；UMM 选择直接、可见、可恢复的真实游戏目录管理。其“插件方案”只保存 `.dll` / `.dll.disabled` 启停快照，并通过预检与失败回滚切换，不会挂载虚拟盘、复制 DLL 或改写 BepInEx 配置。

UMM 的网页协议为 `umm://`，例如 `umm://install/42`。当前 unmod.online 页面尚未主动生成该链接时，用户仍可在 UMM 内的“插件社区”搜索并安装；协议不会绕过登录、依赖检查或安装确认。

---

## Vibecoding：坦诚的人机协作

UMM 从一开始就是一次坦诚的人机协作实践。人类提出愿景、审美和真实需求，AI 协作者参与架构推演、代码实现、问题诊断和文档整理，再由维护者进行选择、试用和验收。

我们不会把 AI 参与隐藏在“全自研”的叙事后面，也不会把 AI 输出当作天然正确的答案。v2.0 将 OpenAI GPT（Codex）补充进协作者名单，正是为了如实记录这段开发过程。Gemini、Claude、Kimi、Cherry Claw 等协作者也在不同阶段留下了贡献。

Vibecoding 对这个项目的意义，不是“让 AI 替人负责”，而是让一个原本可能因经验、时间和信心不足而停留在想法里的社区项目真正落地。最终的产品取舍、文件操作、许可证、发布和用户责任仍由项目维护者承担。

---

## 构建与测试

### 环境要求

- Windows 10/11 x64；
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
git clone https://github.com/YU80Rice/UnturnedModManager.git
cd UnturnedModManager
dotnet build -c Release
dotnet test .\UnturnedModManager.Tests\UnturnedModManager.Tests.csproj
```

生成 Windows x64 自包含单文件：

```powershell
dotnet publish -c Release -r win-x64 `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --self-contained true
```

产物位于 `bin/Release/net8.0-windows/win-x64/publish/UnturnedModManager.exe`。

---

## 许可证：GNU GPL v2.0 only

UMM v2.0 起使用 [GNU General Public License version 2](./LICENSE)，SPDX 标识为 `GPL-2.0-only`。这是一份强 copyleft 许可证：

- 可以运行、研究、修改和再分发本项目；
- 对外分发本项目或基于本项目形成的 GPL 衍生版本时，必须继续使用 GPL v2，并按许可证要求提供相应源代码与许可声明；
- 仅在个人或组织内部使用、且没有向外分发的私人修改，不会因为 GPL 自动产生公开发布义务；
- GPL 保障的是软件自由与源代码权利，并不等同于禁止收费；商业分发仍必须完整遵守 GPL；
- WPF-UI、BepInEx、DXVK、UML 等第三方项目继续适用各自的许可证，UMM 的 GPL 不会改写其原许可证。

从历史 MIT 版本获得的代码仍受当时许可约束；v2.0 及之后由本仓库发布的新变更按 `GPL-2.0-only` 提供。

---

## 贡献者与协作者

| 贡献者 | 贡献 |
|---|---|
| [YU80Rice](https://github.com/YU80Rice) | 项目发起、产品方向、功能需求、验收与社区运营 |
| [Ayndpa](https://github.com/Ayndpa) | UML 作者；为 v2.x 提供重要设计参考；通过 UMM PR #7 直接贡献滚轮交互修复 |
| OpenAI GPT（Codex） | v2.x 架构重构、交互完善、任务中心与图片预览、缓存与会话可靠性、自动化测试、发布验证及文档校订 |
| Gemini、Claude、Kimi、Cherry Claw | 不同阶段的架构讨论、问题分析与代码协作 |

同时感谢：

- [Smartly Dressed Games](https://smartlydressedgames.com/) 与 Nelson Sexton；
- [BepInEx Team](https://github.com/BepInEx)；
- [doitsujin](https://github.com/doitsujin)；
- [lepoco](https://github.com/lepoco)；
- PCL2、HMCL 及其他持续改善启动器体验的开源项目。

AI 协作不替代项目维护者的责任。功能取舍、许可证选择、发布和最终验收由项目维护者决定。

---

## 反馈

- [Bug 报告](https://github.com/YU80Rice/UnturnedModManager/issues)
- [功能建议](https://github.com/YU80Rice/UnturnedModManager/discussions)
- [版本发布](https://github.com/YU80Rice/UnturnedModManager/releases)

<p align="center">
  <sub>Copyright © 2026 YU80Rice and contributors · GPL-2.0-only</sub>
</p>
