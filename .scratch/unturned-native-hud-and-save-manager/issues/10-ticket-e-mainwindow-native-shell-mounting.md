# Ticket E: MainWindow 原生 Shell 生产挂载与现有 7 大业务页面平稳集成 (MainWindow Native Shell Mounting)

**What to build:** 在主窗口 `MainWindow` 中接入 Unturned 原生 UI Shell（左侧 5 槽位大药丸导航栏、顶部沉浸状态栏、左下角环境社区中枢、右下角吉祥物展台）。以单一主内容宿主（Single Content Host）无损迁入 7 大现有业务页面（HomePage、DataCenterPlaceholderPage、PluginWorkshopPage、SettingsPage、TaskCenterPage、AboutPage 及自治详情）。对接 `NavigationShellHost`、`HomeSnapshotCoordinator` 与设计代币（`UnturnedNativeTokens.xaml`），落实浮层遮罩点击安全消费、全键盘 Esc 与焦点环控制。

**Input contracts:**
- 严格保持现有 Page 的继承体系、DataContext 绑定与对应 ViewModel 业务逻辑不改；
- 必须在 Ticket A、B、C、D 全部完成并通过契约测试后方可启动；
- 保持 Git 提交检查点（标记 `pre-native-shell-switch`）作为版本演进防线；
- 主程序可执行文件名永久保持原名 `UnturnedModManager.exe`，严禁产生名为 V2 的文件。

**Blocked by:** 
- Ticket A: NavigationShell 契约与逻辑宿主
- Ticket B: Legacy NavigationFacade 兼容桥
- Ticket C: PluginWorkshopPage 聚合挂载
- Ticket D: HomeSnapshot Coordinator 与 Adapter

**Status:** ready-for-agent

## 范围外 (Out of Scope)
- 严禁接入真实角色/世界本地存档读写（单独立项）；
- 严禁修改业务 ViewModel 内部的数据处理逻辑。

## 验收标准 (Acceptance Criteria)
- [ ] `MainWindow.xaml` 结构重构为 Unturned 原生 Shell，接入 5 槽位大药丸与 HUD 设计代币
- [ ] 单一主内容容器成功挂载 7 大页面，槽位切换平滑无闪烁，无双重历史栈
- [ ] 左下角中枢微卡片实现 Esc 关闭、外部遮罩点击消费（不触发背景控件）与焦点环恢复
- [ ] 首页成功绑定 `HomeSnapshot` 只读投影，小助理展台支持动态安全 Insets 与开关联动
- [ ] 主程序构建输出文件名严格保持为 `UnturnedModManager.exe`，0 编译错误，0 新增代码警告
