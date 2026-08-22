# UMM / UML 技术交接文档

> 面向首次接手本目录、不了解历史的 Agent。本文记录截至 2026-08-22 的源码与发布状态；未经本地或线上复核的内容会明确标注，不应被当作运行时验收结论。

## 1. 先读结论

本目录并不是一个单一仓库，而是两个并列、相互独立的 Git 仓库：

```text
启动器/
├─ UnturnedModManager/       # 主项目，简称 UMM，应继续维护此项目
├─ unturned-mod-loader/      # 开源参考项目，简称 UML，不是 UMM 的子目录或依赖
└─ TECHNICAL_HANDOVER.md     # 本文
```

历史目标不是把 UML 合并、改名或重写成 UMM，而是：**在保留 UMM 的 Windows/WPF/.NET 8 和真实游戏目录管理策略的前提下，吸收 UML 可借鉴的社区浏览、账户登录、详情导航、安装安全和交互设计经验。**

最重要的架构决策：**不要把 UML 的 WinFsp 虚拟盘/虚拟文件系统方案移植到 UMM。** 已知 Unity、Unturned、BepInEx 与 Steam 相关读取在虚拟文件系统下存在兼容性风险。UMM 的插件方案只记录启停状态，直接操作真实游戏目录。

## 2. 当前基线与仓库纪律

| 项目 | UMM（主项目） | UML（参考项目） |
| --- | --- | --- |
| 本地目录 | `UnturnedModManager` | `unturned-mod-loader` |
| 当前提交 | `cf33c960dd25c69fb6835a47063151dbb5b74049` | `bea6b1420ac325bc7e79598421df269946a15185` |
| 当前版本 | `v2.1.8` / 文件版本 `2.1.8.0` | `1.0.6` |
| GitHub | `YU80Rice/UnturnedModManager` | `Ayndpa/unturned-mod-loader` |
| UI / 运行时 | WPF + WPF-UI 3.0.5 / .NET 8 | Avalonia 12.1 / .NET 10 |
| 许可证 | `GPL-2.0-only` | Unlicense（公有领域声明） |
| 角色 | 需要继续开发与发布的产品 | 仅作设计与实现参考，保留原许可 |

UMM 当前存在由此前工作保留的未跟踪目录/文件，例如 `.qa/`、`UMM-V2.1.4-Promo/` 与多个 `audit/` 报告。它们不属于本次文档修改，不得通过 `git clean`、重置、批量删除或“顺手提交”处理。提交前必须以 `git status --short` 建立白名单，只暂存本次明确改动。

UML 的 `winfsp-reference` 一级子模块已在提交 `e3ecc89...`，但其嵌套 `winfsp-reference/ext/test` 仍显示未初始化（`-6ac65cda...`）。因此可以称 UML 主项目可参考、可构建；**不得**称“其所有递归子模块完整”。

## 3. UMM 的产品边界

UMM 是 Windows 上的 Unturned 启动、BepInEx 插件管理与 `unmod.online` 社区客户端。它不包含游戏资产，不修改游戏可执行文件，也不是 Smartly Dressed Games、Valve、BepInEx 或 unmod.online 的官方产品。

已实现的主要能力：

- 自动探测或手动选择 Steam Library 中的 Unturned；模组模式使用 `Unturned.exe -NoBattlEye`，原版模式使用 `Unturned_BE.exe`。
- 安装、修复、升级、停用或卸载 BepInEx 5.4.23.5（`win_x64`、Unity Mono、winhttp doorstop）；停用只切换 `winhttp.dll` / `winhttp.dll.disabled`，卸载保留插件、配置、缓存、日志和社区安装记录。
- 浏览、筛选、排序、搜索 unmod.online 社区条目；列表进入独立详情页，详情的返回必须还原真正来源页，而不是固定回社区首页。
- 浏览器网页登录，应用在本机 `localhost` 回调中接收令牌；人机验证仅由用户在网页完成。
- 安全安装、更新、依赖安装和卸载社区插件；本地扫描、启用/停用、拖入 DLL/ZIP 导入、社区条目匹配、插件方案切换。
- 下载/安装任务中心、失败原因、有限操作历史、当次运行重试、社区缓存、右下角通知、启动器更新检查与人工确认安装。
- DXVK 2.4 可选部署与诊断建议；它是兼容性/性能分析辅助，不得承诺特定显卡或系统一定改善性能。
- 深色/浅色/跟随系统、七套持久化配色，以及窗口尺寸/位置、侧栏状态、账户显示和新手引导状态的持久化。

### 明确不再做的事

- 不引入 WinFsp、虚拟盘挂载、overlay 或“挂载式多 profile”。
- 不在启动器启动时强制安装或覆盖 `LaunchPerfOptimizer`、`WaterPerfOptimizer`；它们后续如需整合，必须重新设计并验收。
- 不用昵称、安装记录或客户端猜测社区身份；身份只能使用 unmod.online 服务端返回的数据。
- 不绕过 TLS 或证书吊销检查上传 GitHub Release 资产。
- 不将“编译成功”“单元测试通过”“已注册协议”表述为真实游戏、社区网络或完整 UI 验收通过。

## 4. UMM 关键结构与进入路径

```text
UnturnedModManager/
├─ App.xaml.cs                         # 应用启动、单实例/协议处理、服务生命周期
├─ MainWindow.xaml.cs                  # WPF 主窗口、导航和窗口交互
├─ Pages/                              # Home / Community / CommunityDetail / ModList / Settings / TaskCenter / About
├─ ViewModels/                         # 页面状态、命令、导航来源上下文
├─ Services/                           # 业务边界（见下表）
├─ Models/                             # API、安装清单、任务、主题等数据模型
├─ UnturnedModManager.Tests/           # 业务与主题对比度等自动化测试
├─ publish/UMM-v2.1.8-win-x64/         # 当前发布候选目录
├─ README.md / CHANGELOG.md / LICENSE  # 对外说明、版本记录与 GPL-2.0-only
└─ audit/                              # 各轮实现/修复可追溯报告
```

`App.xaml.cs` 创建 `AppServices`；`Services/AppServices.cs` 是组合根。页面不应自行 new 长生命周期服务，应通过构造参数或既有 ViewModel 工厂取得显式依赖。导航状态由 `AppNavigationService` 维护，任何“从本地插件跳到社区详情”的新交互都必须保留来源上下文，返回键回到真实上一级。

| 服务 | 责任与约束 |
| --- | --- |
| `ThemeService` | 主题、配色和语义资源。新控件优先绑定动态主题资源，主按钮文字必须使用 `TextOnAccentFillColorPrimaryBrush`，不可硬编码黑/白。 |
| `CommunityAuthService` | 浏览器登录、本机回调、令牌恢复和服务端会话验证。人机验证不在客户端伪造或绕过。 |
| `CommunityApiClient` / `CommunityCacheService` | 社区 HTTP API 和缓存。离线缓存账户不等于已验证会话，受保护操作必须保持禁用。 |
| `CommunityModInstaller` | 依赖递归安装、清单、文件所有权、冲突、回滚和卸载前哈希校验。它是插件文件写入的关键边界。 |
| `LocalModService` / `PluginProfileService` | 本地 DLL 扫描、启停、导入、社区映射与启停快照方案。方案应用只能切换启停，不能复制 DLL、覆盖配置或挂载虚拟盘。 |
| `OperationTaskCenter` | 操作进度、失败原因和 `task-history.json`。重试委托只保证本次启动会话有效。 |
| `BepInExService` | 固定版本环境的下载源、完整性校验、安装/修复/卸载。 |
| `GamePathService` / `GameLaunchService` | 游戏探测、路径选择、原版/模组模式启动。 |
| `DxvkService` / `DiagnosticService` | 可选 DXVK 与日志/诊断包；诊断包写入启动器同级目录，并从通知打开所在目录。 |
| `LauncherUpdateService` | GitHub Release 检查、下载、摘要读取、校验和显式确认更新。替换前保留 `.bak`。 |
| `SingleInstanceService` / `ProtocolRegistrar` | 单实例唤醒与 `umm://install/{id}` 协议；协议只打开详情并要求用户确认，不得绕过安装安全流程。 |
| `UserNotificationService` | 右下角短通知，最多三条，避免用阻塞弹窗替代常规结果反馈。 |

## 5. 账户、配置与敏感数据

默认用户数据根为：

```text
%AppData%\Roaming\UnturnedModManager
```

启动前设置 `UMM_DATA_DIRECTORY` 可替代**整个**数据根，用于隔离验收、便携调试或新用户测试。发布包目录本身不是配置来源；发现“新目录仍读到配置”时，先检查该环境变量与当前 Windows 用户的 AppData，而不是假设程序未清理干净。

关键数据包括：

- `config.json`：游戏路径、主题、窗口状态、社区登录令牌、账户显示信息等；视为敏感文件。
- `community-mods/`：社区插件安装清单、文件所有权与备份。
- `plugin-profiles/`：按游戏安装目录隔离的插件方案。
- `task-history.json`：任务中心历史。
- 社区分类、列表、详情元数据缓存：网络暂时不可用时可用，但必须标注其缓存状态。

不得把上述目录、令牌、Cookie、含 token 的回调 URL、诊断包或真实账户截图提交到 Git、上传 Issue 或输出到公共日志。登录回调路径是 `/callback?token=...`；令牌写入后仍须调用社区端恢复/验证接口，不能只依据 JWT 字符串认为登录成功。

## 6. 安装与文件安全不变量

已实现的保护必须按入口区分，不能把本地导入的限制误称为社区安装器已有能力：

1. **社区包安装器**会校验 ZIP 路径、拒绝路径穿越、限制条目数量与解压总量，并拒绝覆盖 `Unturned.exe`、`Unturned_BE.exe`、`UnityPlayer.dll` 三个受保护游戏根文件；同时具有文件所有权冲突阻止、更新回滚和卸载前哈希校验。
2. **本地拖入 ZIP 导入器**要求合法 `BepInEx/plugins` 或 `BepInEx/config` 目录结构、至少一个插件 DLL，并拒绝 `BepInEx/core`、Doorstop 等其他区域；拖入 DLL 也只安装到允许的插件路径。
3. 社区安装器当前尚未实施与本地 ZIP 导入器同等的 `BepInEx/plugins` / `BepInEx/config` 目录白名单，理论上仍可能向游戏目录中其他未受保护的相对路径写入文件。这是高优先级安全债务：在扩展社区安装功能前，应先将目录白名单、DLL 存在要求及相应的单元测试移入 `CommunityModInstaller`，并对既有社区包兼容性做验证。
4. BepInEx 包无论来自社区、国内镜像或 GitHub，均必须走同一 SHA-256 完整性校验。已登录优先社区包；未登录会跳过社区源，再尝试镜像，最后 GitHub 官方源。

上述保护不是第三方代码沙箱。插件来源可信度和真实游戏兼容性仍由用户和维护者确认。

## 7. 主题与 UI 回归约束

UMM 的视觉系统不应再通过固定坐标、叠加多个不可滚动容器、或每页硬编码文字颜色实现。新页面优先使用网格、相对尺寸、页面滚动容器和 Theme 资源。

- 主题偏好：浅色、深色、跟随系统；当前“跟随系统”依据启动时系统设置，运行期间自动监听变化仍需真实验证/必要时补强。
- 配色顺序与集合：Fluent、暖米白、吉祥物橙、松林雾绿、深海雾蓝、克莱因蓝、夜雾紫。
- 普通文字使用 `TextFillColorPrimaryBrush` / Secondary / Tertiary 等动态资源；页面内的普通 `TextBlock` 不得遗留深色固定前景。
- 主题服务对主强调按钮的默认、悬停、按下都提供可读文字。新增 `Appearance=Primary` 按钮时必须显式复用 `TextOnAccentFillColorPrimaryBrush`。
- 列表/详情采用“列表 -> 独立详情”的导航，不在同一屏固定留出无用途的大空白详情栏；小窗口、收起侧栏和超长身份/用户名要用弹性布局与省略号，并提供悬停查看完整文本。
- 鼠标位于任意嵌套区域时，滚轮应交给最近可滚动容器。视觉改动需要在浅色、深色、每一种配色以及不同窗口尺寸下做真实 UI 冒烟。

## 8. UML 可学习项与不可移植项

UML 值得持续参考的内容包括：Avalonia 的组件划分、社区列表/详情信息结构、缩略图与图片预览、登录窗口、引导、设置、国际化、更新入口，以及 `ProfileService` 对方案概念的表达。借鉴时应复述需求并在 UMM 现有 Pages + ViewModels + Services 边界内独立实现，而不是复制粘贴或引入其许可之外的代码。

UML 的关键技术差异在 `Services/VirtualFilesystemService.cs` 和 `Services/ProfileService.cs`：启动时创建/挂载 WinFsp 虚拟文件系统，将 profile overlay 与真实游戏目录合并后从虚拟盘启动游戏。此行为与 UMM 的“真实目录 + 启停快照”决策冲突，禁止复用。

## 9. 构建、测试与验收命令

UMM 开发环境要求 Windows 10/11 x64 与 .NET 8 SDK。常用命令：

```powershell
Set-Location "D:\Agent-工作目录\DevelopMyUNMultiplayerModAndModloader\启动器\UnturnedModManager"
dotnet build -c Release
dotnet test .\UnturnedModManager.Tests\UnturnedModManager.Tests.csproj
dotnet publish -c Release -r win-x64 `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --self-contained true
git diff --check
git status --short
```

构建与测试是代码门禁，不是完整验收。高风险改动还必须在隔离的 `UMM_DATA_DIRECTORY` 下做真实 UI、社区网络、BepInEx 安装/启停/卸载、插件安装更新回滚、拖放、协议入口以及实际 Unturned 启动检查。任何源代码或 DLL 变化后，都必须重新绑定测试证据到新的 Git 提交和 EXE SHA-256。

## 10. 发布状态与待办

已验证发布状态：UMM 的 `v2.1.8` 标签、源码提交和中文 GitHub Release 文本已经存在，远端 `main` 对应 `cf33c960...`。

尚未完成的发布动作：GitHub Release 当前没有 EXE 资产。待网络/证书环境恢复后，只能把下列现有文件上传到**既有** `v2.1.8` Release，不要新建标签、重复 Release 或降低证书校验：

```text
本地文件：UnturnedModManager\publish\UMM-v2.1.8-win-x64\UnturnedModManager.exe
Release 资产名：UnturnedModManager-v2.1.8-win-x64.exe
文件版本：2.1.8.0
SHA-256：91F075BDAAFAD1EF2DC1CF03E8C0BFCFAF0342FC09D94155F739A9C11F8EB305
```

此前上传被 Windows 证书吊销检查错误和工具网络时限阻断；这是外部发布通道问题，不是“资产已上传”的证据。上传成功后必须用 GitHub API/网页核对资产名、大小和下载后 SHA-256。

建议后续优先级：

1. 补传并核验 v2.1.8 EXE 资产。
2. 建立隔离用户数据的真实 UI 冒烟清单，覆盖所有主题/配色、窗口尺寸、侧栏折叠、滚动和返回上下文。
3. 优先为 `CommunityModInstaller` 实施并回归测试目录白名单，令社区包与本地 ZIP 导入同样限制在 `BepInEx/plugins` 与 `BepInEx/config`，再扩大社区安装功能。
4. 对社区登录回调、身份显示、GitHub Release 下载回退、BepInEx 三来源与插件更新/卸载回滚做可重复网络验收。
5. 仅在现有行为稳定后，继续补充可访问性、键盘导航、错误恢复与“跟随系统”运行时变更响应；不要以框架迁移替代这些基础质量工作。

## 11. 对接时的最低报告标准

任何后续 Agent 的报告至少应给出：需求项到代码位置的映射、改动文件白名单、构建命令与结果、测试命令与结果、当前提交/产物 SHA-256，以及哪些仅为静态证据、哪些已有真实运行证据、哪些仍未验证。若出现安装器、登录、发布或游戏启动问题，应保留日志与环境条件，不能用猜测或 UI 截图单独宣告修复完成。
