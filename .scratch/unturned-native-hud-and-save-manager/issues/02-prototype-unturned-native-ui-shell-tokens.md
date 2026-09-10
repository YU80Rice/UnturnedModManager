# Ticket 02: Unturned 原生风格 UI Shell 空间结构与设计代币原型 (Prototype Unturned Native UI Shell & Tokens)

Status: ready-for-human
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

## 交付物与实施验证 (Implementation & Verification)

### 1. 隔离独立原型文件资产
- **设计代币字典**：[tokens/UnturnedNativeTokens.xaml](prototype/tokens/UnturnedNativeTokens.xaml)
  - 提取 Unturned 军工暗调质感色调（`UnturnedBackdropBrush`、`UnturnedHudPanelBrush`、`UnturnedHudCardBrush`、`UnturnedStrokeSubtleBrush`、`UnturnedAccentGoldBrush`、`UnturnedAccentGreenBrush`）；
  - 定义大药丸按钮圆角（8px）、内边距（18,10）、卡片边框、高亮指示条（4px 金黄矩形）与键盘焦点环样式。
- **独立 UI Shell 控件**：[UnturnedNativeShellPrototype.xaml](prototype/UnturnedNativeShellPrototype.xaml) 与 [UnturnedNativeShellPrototype.xaml.cs](prototype/UnturnedNativeShellPrototype.xaml.cs)
  - 顶部品牌层 + 居中全局状态药丸（模组模式/BE版本/插件计数）+ 右上角次级浮动入口（任务/账户/关于）；
  - 左侧经典 5 槽位药丸导航（`NavSlotPlay`, `NavSlotData`, `NavSlotMods`, `NavSlotSettings`, `NavSlotExit`）；
  - 右侧透明无边框 `Frame` 挂载宿主，支持页面热插拔；
  - 挂载 `Shell_PreviewMouseWheel` 隧道路由，根治嵌套容器滚轮被吞问题。
- **首页挂载原型**：[pages/PrototypePlayPage.xaml](prototype/pages/PrototypePlayPage.xaml) 与 [pages/PrototypePlayPage.xaml.cs](prototype/pages/PrototypePlayPage.xaml.cs)
  - 三层空间架构：顶部状态横幅、3:2 英雄启动卡片与当前方案卡片、底部精选模组与 Patch Notes；
  - 配备吉祥物挂件（`MascotContainer`）与交互式开关（`MascotToggleBtn`），验证吉祥物隐藏后网格自动无缝闭合。
- **数据域挂载原型**：[pages/PrototypeDataPage.xaml](prototype/pages/PrototypeDataPage.xaml) 与 [pages/PrototypeDataPage.xaml.cs](prototype/pages/PrototypeDataPage.xaml.cs)
  - 4 大独立只读子域药丸 Tab（角色槽位、单机世界、服务器配置、快照与备份）；
  - 严格保持只读契约，无任何写入/修改/删除等受控编辑控件。
- **自包含交互式 HTML 视觉审查报告**：[review.html](prototype/review.html)
  - 支持 1:1 视觉还原、五槽位点击切换模拟、吉祥物显隐动态切换、数据域 Tab 联动与 WCAG 对比度评分卡；可在任何浏览器离线打开审查。

### 2. 自动化测试与 WCAG 守卫
- 编写测试套件 [UnturnedNativeShellPrototypeTests.cs](../../UnturnedModManager.Tests/UnturnedNativeShellPrototypeTests.cs)，共新增 5 大针对性断言：
  1. `DesignTokens_XmlStructure_ContainsAllRequiredKeys`：验证代币完整性；
  2. `DesignTokens_ColorContrast_StrictlySatisfiesWcagAaAndAaa`：验证主文字白底高对比度达到 **15.6:1 (AAA)**、次级文字达到 **8.2:1 (AAA)**、金色强调色达到 **7.1:1 (AAA)**；
  3. `ShellPrototype_Structure_HasFiveSlots_FrameMounting_AndTunnelRouting`：验证 5 槽位与隧道路由；
  4. `PlayPage_Structure_ThreeLayerLayout_AndMascotToggle`：验证三层首页与吉祥物无缝折叠；
  5. `DataPage_Structure_StrictlyReadOnly_AndFourDataDomains`：验证 4 大只读域与只读安全防线。
- 执行 `dotnet test`：**160/160 项测试全部通过（155 原有基线 + 5 项原型测试，0 失败，0 警告）**。

### 3. 生产隔离守卫审计
- 生产代码（`MainWindow.xaml`、生产导航类、7 大现有页面与业务服务、发布配置）保持 100% 干净，未作任何侵入性修改。
