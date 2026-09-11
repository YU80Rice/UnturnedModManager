# Ticket 02: Unturned 原生风格 UI Shell 空间结构与设计代币原型 (Prototype Unturned Native UI Shell & Tokens)

Status: resolved
Type: prototype
Blocked by: 

> **PM 硬门槛约束**：本工单仅限在 `.scratch/.../prototype/` 目录下构建独立隔离技术原型，严禁修改生产 `MainWindow.xaml`、生产导航服务、业务逻辑、版本号或发布配置；原型必须验证五槽位 Shell、设计代币、`Page + Frame` 挂载、DPI 缩放、滚动不被吞、键盘焦点和 WCAG AA，提交可供人工视觉审查的结果。不提前冻结最终契约（留待 Ticket 03）。

## 问题

针对 PM 强调的“先建立可替换的 Unturned 风格 Shell，不同时重写现有业务服务，避免视觉重构与功能重构互相拖累”以及“借鉴空间结构、暗色半透明面板、左侧大按钮与背景氛围，但不做逐像素死板复制，保持可访问性”：

1. **设计代币（Design Tokens）体系构建**：
   - 提取 Unturned 原版主菜单的核心视觉变量：左侧大药丸按钮圆角、黑色轮廓描边、暗调半透明面板背景色（`#14181B` 与透明度）、经典几何图标风格、强调色高亮边框；
   - 确保暗色文字与背景在 WCAG 2.1 AA 守卫下达到 4.5:1 / 7:1 高对比度合规。
2. **轻量可替换 Shell 原型构建**：
   - 构建一个独立的 `UnturnedNativeShell` 原型容器（顶部状态栏 + 左侧大药丸导航栏 + 右侧 HUD 浮动主舞台 + 背景壁纸透出）；
   - 验证其如何作为一个可插拔的展示层，能够无缝挂载现有的 `Frame` / 页面切换逻辑，而不破坏底层任何 ViewModel 与 Service。
3. **输出要求**：
   - 建立 XAML 样式资源或原型视窗，产出原型截图/可测试组件供人工视觉审查。

---

## PM 视觉验收与一轮原型修订说明 (Revision 1)

### 1. PM 视觉审查结论
- **视觉语言：通过**。暗色 HUD、金色高亮选中态、左侧大药丸结构、角色数据只读分区与层级质感符合目标。
- **功能映射：需补齐核心启动前操作闭环**。此前首页仅有只读状态展示，遗漏了玩家启动前的关键操作能力（BepInEx 安装/修复/开关、DXVK 开关/显卡提示、导出脱敏诊断包）。
- **口径纠偏**：
  - 测试结果表述为：“测试 160/160 通过；未发现编译错误；运行环境产生 NETSDK1057 preview SDK 提示”。
  - 明确 Page+Frame 热插拔、滚轮、键盘焦点与 DPI 在当前测试中属于静态/原型结构证据，不提前宣称真实 WPF 运行时验收。

### 2. 修订落地内容
1. **五槽位规范命名对齐**：
   - `开始游戏` → `游戏启动`
   - `角色设定` → `角色与存档`
   - `创意工坊` → `插件工坊`（聚合本地与 unmod.online 社区插件）
   - `游戏设置` → `启动器设置`
   - `退出游戏` → `退出 UMM`（明确不误关正在运行的游戏）
2. **首页“环境与诊断”卡片落地**：
   - 在 `PrototypePlayPage.xaml` 右侧新增 `EnvDiagnosticsCard`；
   - **BepInEx 模块**：版本状态指示 + 启用/停用复选开关 + `[🔧 检查并修复]` 紧凑按钮；
   - **DXVK 模块**：启用/停用复选开关 + `NVIDIA GeForce RTX 4070 (支持 Vulkan 1.3)` 适配提示；
   - **诊断模块**：运行异常与冲突摘要 + `[📦 导出脱敏诊断包]` 入口；
   - 形成完整的启动前闭环：`检查环境 → 调整开关 → 查看兼容提示 → 启动游戏 → 出错后导出诊断`。
3. **插件推荐文案调整**：
   - 改为“精选插件 / 社区推荐”，BUE 标定为“社区认证 BepInEx 插件”，与 Steam Workshop 解绑。
4. **评审页与测试用例同步**：
   - `review.html` 补齐交互式开关联动与操作反馈；
   - `UnturnedNativeShellPrototypeTests.cs` 同步更新断言并全部通过。

---

## 交付物与实施验证清单 (Artifacts & Verification)

### 1. 隔离独立原型文件资产
- **设计代币字典**：[tokens/UnturnedNativeTokens.xaml](prototype/tokens/UnturnedNativeTokens.xaml)
  - 提取 Unturned 军工暗调质感色调（`UnturnedBackdropBrush`、`UnturnedHudPanelBrush`、`UnturnedHudCardBrush`、`UnturnedStrokeSubtleBrush`、`UnturnedAccentGoldBrush`、`UnturnedAccentGreenBrush`）；
  - 定义大药丸按钮圆角（8px）、内边距（18,10）、卡片边框、高亮指示条（4px 金黄矩形）与键盘焦点环样式。
- **独立 UI Shell 控件**：[UnturnedNativeShellPrototype.xaml](prototype/UnturnedNativeShellPrototype.xaml) 与 [UnturnedNativeShellPrototype.xaml.cs](prototype/UnturnedNativeShellPrototype.xaml.cs)
  - 顶部品牌层 + 居中全局状态药丸（只读摘要）+ 右上角次级浮动入口（任务/账户/关于）；
  - 左侧 5 槽位规范药丸导航（`游戏启动`、`角色与存档`、`插件工坊`、`启动器设置`、`退出 UMM`）；
  - 右侧透明无边框 `Frame` 容器挂载宿主，支持页面热插拔；
  - 挂载 `Shell_PreviewMouseWheel` 隧道路由，防止嵌套容器滚轮被吞。
- **首页挂载原型 (含环境诊断闭环)**：[pages/PrototypePlayPage.xaml](prototype/pages/PrototypePlayPage.xaml) 与 [pages/PrototypePlayPage.xaml.cs](prototype/pages/PrototypePlayPage.xaml.cs)
  - 状态横幅 + 英雄启动卡片 + 环境与诊断卡片（安装修复/开关/DXVK/诊断包）+ 吉祥物平滑折叠 + 精选推荐与更新日志。
- **数据域挂载原型**：[pages/PrototypeDataPage.xaml](prototype/pages/PrototypeDataPage.xaml) 与 [pages/PrototypeDataPage.xaml.cs](prototype/pages/PrototypeDataPage.xaml.cs)
  - 4 大独立只读子域药丸 Tab（角色槽位、单机世界、服务器配置、快照与备份）；
  - 严格保持只读契约，无任何写入/修改/删除等受控编辑控件。
- **自包含交互式 HTML 视觉审查报告**：[review.html](prototype/review.html)
  - 支持 1:1 视觉还原、五槽位点击切换模拟、环境诊断动态开关与修复模拟、数据域 Tab 联动与 WCAG 对比度评分卡。

### 2. 自动化测试与 WCAG 守卫
- 执行测试套件 [UnturnedNativeShellPrototypeTests.cs](../../UnturnedModManager.Tests/UnturnedNativeShellPrototypeTests.cs)：
  1. `DesignTokens_XmlStructure_ContainsAllRequiredKeys`：代币完整性验证；
  2. `DesignTokens_ColorContrast_StrictlySatisfiesWcagAaAndAaa`：主文字高对比度达到 **15.6:1 (AAA)**，次级文字达到 **8.2:1 (AAA)**，金色强调色达到 **7.1:1 (AAA)**；
  3. `ShellPrototype_Structure_HasFiveSlots_FrameMounting_AndTunnelRouting`：验证规范命名五槽位与隧道路由；
  4. `PlayPage_Structure_LaunchClosedLoop_AndDiagnosticsCard`：验证环境与诊断闭环操作、开关、修复与推荐；
  5. `DataPage_Structure_StrictlyReadOnly_AndFourDataDomains`：验证 4 大只读域与只读安全防线。
- 执行 `dotnet test`：**测试 160/160 通过；未发现编译错误；运行环境产生 NETSDK1057 preview SDK 提示**。

### 3. 生产隔离守卫审计
- 生产代码（`MainWindow.xaml`、生产导航类、7 大现有页面与业务服务、发布配置）保持 100% 干净，未作任何侵入性修改。

---

## PM 终审与二轮全域同步 (Revision 2 - Final Synchronization)

### 1. 关单刚性同步准则达成
根据 PM 关单要求：“不能只把 HTML 捏满意。最终决定的设计还要同步到：UnturnedNativeShellPrototype.xaml、PrototypePlayPage.xaml、PrototypeDataPage.xaml、UnturnedNativeTokens.xaml 以及对应原型测试”，已完成全部同步映射：
1. **独立小助理展台从公告卡片解耦**：
   - 公告卡片恢复纯净全宽排版，彻底移除旧版卡片内嵌的挂件与按钮；
   - 页面右下角独立挂载悬浮式 `MascotAssistantContainer`，接入真实高清资源 `/Assets/umm-mascot-chibi-v2.png`，支持点击趣味对白轮播彩蛋；
2. **顶栏与小助理通信联动**：
   - 顶栏右侧新增 `SecondaryMascotBtn`（`[🦊 助理]` 胶囊按钮），点击跨组件联动展开/收起小助理展台；
3. **左侧导航栏中枢微卡片**：
   - 在退出按钮上方增加 `NavHubTriggerBtn`（`[💻 YU80Rice · Steam 就绪 🟢]`），打通本机环境、Steam 状态与社区中枢操作闭环；
4. **代币体系补充**：
   - `UnturnedNativeTokens.xaml` 补充小助理暖橙画刷（`UnturnedMascotOrangeBrush`、`UnturnedMascotBadgeBrush`）；
5. **测试套件扩充与全绿**：
   - `UnturnedNativeShellPrototypeTests.cs` 新增代币、结构与代码后置契约测试用例，断言总数增至 **162/162 全通过**。

### 2. 状态更新与最终验收归档 (2026-09-11)
- **Status: resolved**
- **用户验收日期**：2026-09-11（PM 与用户复核通过，准予关单）
- **核准评审基线**：
  - 交互式模拟器：[review.html](prototype/review.html)（版本对应 Commit `872c7a0` / `76c6563`）
  - 最终同步交付报告：[walkthrough.md](file:///C:/Users/The%20New%20Age/.gemini/antigravity/brain/3c0f2ce9-ae71-4315-840d-e53551e2266c/walkthrough.md)
- **硬性边界声明**：
  - 本工单交付的 HTML 评审模拟器与 WPF XAML 原型系统（`UnturnedNativeShellPrototype`、`PrototypePlayPage`、`PrototypeDataPage`、`UnturnedNativeTokens`）均为**视觉与交互沙盒原型证据**；
  - 真实生产 `MainWindow.xaml` 与 7 大生产业务页面**尚未迁移**，生产代码 100% 零侵入；
  - 最终 `NavigationShell` 接口契约留待 Ticket 03 严格开展。
- **自动化测试指标**：`162/162` 单元测试全部通过（0 失败、0 跳过；运行环境产生 `NETSDK1057` preview SDK 提示，不视为零警告）。

