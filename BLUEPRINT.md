# UnturnedModManager (UMM) 产品蓝图与架构演进规划

> **规划日期**：2026-08-22  
> **定位标杆**：对标 Minecraft 领域的 Plain Craft Launcher 2 (PCL 2)，打造 Unturned 领域最优雅、最稳定、新手零门槛、深度可定制的现代化模组管理与启动平台。

---

## 1. 产品核心愿景与定位

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                          UnturnedModManager (UMM)                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  🎯 核心定位：新手极致开箱即用 + 社区生态无缝集成 + 专家级诊断与方案隔离       │
│  🛡️ 架构底线：100% 物理文件安全管理（拒斥虚拟盘兼容隐患） + 离线完全自治      │
│  🎨 视觉体验：WPF-UI Fluent 质感 + PCL2 卡片式向导 + 开放式全维度自定义主题   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.1 服务对象与价值主张
1. **普通联机玩家**：
   - **一键配置**：全自动探测 Steam 安装路径，一键安装/修复 BepInEx 运行环境。
   - **一键开黑**：朋友分享一个 `.ummpk` 模组包文件，拖入即可 100% 复刻联机环境（插件版本 + 配置文件）。
   - **一键排障**：游戏崩溃无需抓狂，自动感知异常并给出中文排查建议（缺前置、BE未关、冲突），一键导出脱敏诊断包。
2. **模组创作者与服主**：
   - **方案隔离**：单机测试、服务器 A、服务器 B 拥有完全独立的 plugins 与 config 物理工作区，秒级无损切换。
   - **规范打包**：支持一键将当前方案导出为规范模组包，带元数据与依赖关系。
3. **社区生态 (unmod.online)**：
   - **优质分发源**：提供一键检索、分类筛选、依赖递归解析、安全下载与自动更新。

---

## 2. 核心架构与系统边界

### 2.1 离线自治与社区生态边界
- **离线自治优先 (Offline First)**：
  - 无网或社区服务器宕机时，本地扫描、插件启停、方案切换、模组包导入/导出、原版/模组模式启动 **100% 可用**。
  - BepInEx 与 DXVK 提供多源容灾（社区源 -> 国内镜像 -> GitHub 官方源 -> 本地离线包）。
- **社区生态赋能 (Community Enhanced)**：
  - unmod.online 接入作为首选安全分发网络。
  - 网页 OAuth 授权 + 本机安全回调接收 Token，敏感凭证物理隔离存储于 `%AppData%`。

### 2.2 文件管理机制：彻底抛弃虚拟盘，坚定物理文件方案
- **拒绝 WinFsp / Overlay 虚拟盘**：避免 Unity Mono、Unturned 引擎层与 Steam 路径读取在虚拟文件系统下的崩溃与兼容隐患。
- **方案物理管理与部署引擎 (Physical Profile Engine)**：
  - 本地仓库管理：在数据根目录建立方案独立存储库（`profiles/<ProfileId>/`，包含独立的 `plugins/` 和 `config/`）。
  - 部署同步：切换方案时，启动器以原子操作将选定方案的物理文件同步/部署到游戏的 `BepInEx/plugins` 与 `BepInEx/config`，并清理/归档旧方案差异。
  - 保留单个插件的 `.disabled` 快捷启停，满足微调需求。

---

## 3. 安全沙箱与防护模型

针对社区安装与本地导入，实施统一的严苛安全检查流水线：

```text
输入包 (ZIP / DLL / .ummpk)
   │
   ▼
[ 1. 解压炸弹防御 ] ──> 限制条目 <= 4096，解压总量 <= 1 GB，拒绝压缩率异常
   │
   ▼
[ 2. 路径穿越审查 ] ──> 拒绝 `..`、绝对路径、驱动器盘符、保留设备名
   │
   ▼
[ 3. 严格目录白名单 ] ──> 仅允许写入 `BepInEx/plugins/**` 与 `BepInEx/config/**`
   │                   (严禁向游戏根目录、Unity 数据目录、BepInEx/core 写入任何文件)
   │
   ▼
[ 4. 危险扩展名拦截 ] ──> 严禁包含 `.exe`、`.bat`、`.cmd`、`.ps1`、`.vbs`、`.com`、`.scr` 等可执行载荷
   │
   ▼
[ 5. 核心载荷校验 ] ──> 插件包必须包含至少一个合法 .dll，配置包必须对应合法插件
   │
   ▼
[ 6. 所有权与原子写入 ] ──> 冲突检测 -> 备份当前版本 -> 写入并记录清单 -> 失败自动回滚
```

---

## 4. 模组方案与 .ummpk 模组包规范

### 4.1 方案数据模型 (Profile Specification)
- 方案元数据：ID、名称、描述、作者、版本、封面/图标、创建与修改时间。
- 包含内容：
  - `manifest.json`：插件清单（文件名、社区 RemoteId、版本、SHA-256、启用状态）。
  - `plugins/`：实际插件 DLL 与资源。
  - `config/`：对应的配置文件（`.cfg` / `.json`）。
  - `settings.json`：可选方案级启动参数（如是否启用 DXVK、特定启动参数）。

### 4.2 `.ummpk` (Unturned Mod Manager Package) 标准
- 本质为标准 ZIP 归档，扩展名为 `.ummpk`。
- 启动器支持关联 `.ummpk` 文件类型，支持双击导入、拖拽导入与一键打包导出。

---

## 5. 智能崩溃分析与一键诊断系统

### 5.1 监控与触发机制
1. **主动生命周期感知**：
   - 监听游戏进程启动与退出。
   - 若游戏非正常退出（ExitCode != 0），主界面/通知中心弹出卡片：“检测到游戏异常退出，点击查看分析报告”。
2. **手动一键体检**：
   - 工具箱提供“环境健康度检查”按钮，一键扫描当前环境。

### 5.2 智能诊断特征库 (Diagnostic Rule Engine)
- **特征 1：BattlEye 冲突** -> 模组模式误用了 `Unturned_BE.exe` 或 BattlEye 服务开启导致 BepInEx 注入被拦截。
- **特征 2：缺失前置依赖** -> 解析 `LogOutput.log` 中的 `TypeLoadException` / `FileNotFoundException`，指出缺失的 Mod。
- **特征 3：Mono / Doorstop 未加载** -> 检查 `winhttp.dll` 状态、架构不匹配或权限受限。
- **特征 4：插件版本与游戏不兼容** -> 识别典型 API 弃用报错。
- **特征 5：DXVK 显卡/驱动不兼容** -> Vulkan 初始化失败特征提取。

### 5.3 脱敏诊断包导出
- 自动抓取并合并：系统环境摘要、Unturned 版本、BepInEx 状态、已装插件清单、`Player.log`、`LogOutput.log`、Doorstop 日志。
- **严格脱敏**：自动过滤用户系统用户名、本地完整路径中的敏感目录名、社区 Token 等个人隐私。

---

## 6. 开放式主题与 UI 自定义框架

### 6.1 视觉设计准则
- 基于 **WPF-UI (Fluent Design)**，融合 **PCL2 卡片化布局**。
- 清晰的层级、柔和的阴影、直观的徽章标签、丝滑的过渡动画与状态反馈。
- 响应式弹性布局：兼顾小窗口、大分辨率与侧边栏折叠。

### 6.2 `.ummtheme` 主题包规范
- 支持用户与社区创作者自由制作并分享主题包（`.ummtheme`）。
- 主题包构成：
  - `theme.json`：主题名称、作者、配色板（Primary Accent、Secondary、Background Tint、Card Alpha、Border Radius）。
  - 背景资产：静态壁纸（PNG/JPG/WEBP）或流体渐变配置。
  - 亚克力 / 模糊特效强度配置。
- 启动器内置实时热预览与一键恢复默认。

---

## 7. 蓝图落地演进里程碑 (Roadmap)

### 里程碑验收与规划清单

| 里程碑 | 核心目标与交付物 | 测试项 | 状态 |
| :--- | :--- | :---: | :---: |
| **Phase 1: 基石与安全加固** | `BepInEx/plugins` & `config` 严格目录白名单、单 DLL 自动重定向、危险扩展名集合拦截、压缩包沙箱防护 | 51+ | ✅ **已验收** |
| **Phase 2: 卡片体验与智能崩溃诊断** | 前置缺失/反作弊冲突/Doorstop/DXVK 智能特征分类、日志敏感信息脱敏导出、首页排查建议卡片 | 67+ | ✅ **已验收** |
| **Phase 3: 物理多 Profile 与 .ummpk 模组包** | 物理级改名切换方案、`.ummpk` 标准 ZIP 模组包一键导出/安全导入/拖拽安装 | 74+ | ✅ **已验收** |
| **Phase 4: 开放式主题框架与自定义 UI** | `.ummtheme` 主题包规范、卡片透明度/圆角调节、壁纸资产沙箱解压、主题实时热切换与 WCAG AA 对比度返修 | 85+ | ✅ **已验收** |
| **Phase 5: Windows 原生关联与 Shell 扩展** | 注册 `.ummpk` 与 `.ummtheme` 专属文件类型与图标，双击直接激活单实例并弹出导入确认 | 待定 | 📋 **规划中 (v2.2.0)** |
| **Phase 6: 联机生态与双端网桥防呆校验** | `SteamP2PFriends` 与 `LaunchMultiplayerNet` 深度联动，一键导出联机同步包与底层网桥完整性检查 | 待定 | 📋 **规划中 (v2.2.0+)** |

---

## 8. 版本交付计划 (v2.2.0 Milestone Release)

1. **版本跃迁**：基于 Phase 1~4 全新架构与生态格式，由 `v2.1.x` 跃迁为 **`v2.2.0` 正式里程碑版本**。
2. **核心交付成果**：
   - 物理多方案切换与 `.ummpk` 模组包双向流通；
   - 崩溃特征分类与脱敏诊断包一键导出；
   - 开放式 `.ummtheme` 主题包热切换与 WCAG AA 视觉优化；
   - 社区安装器严苛白名单与安全沙箱防护。
3. **架构资产留档**：
   - 领域模型规约：[`CONTEXT.md`](file:///D:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/CONTEXT.md)
   - 架构决策记录：[`docs/adr/0001-physical-file-management.md`](file:///D:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/docs/adr/0001-physical-file-management.md)、[`docs/adr/0002-safe-sandbox-whitelist.md`](file:///D:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/docs/adr/0002-safe-sandbox-whitelist.md)、[`docs/adr/0003-open-theme-pipeline.md`](file:///D:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/docs/adr/0003-open-theme-pipeline.md)

