# 04: 模组包（.ummpk）显式审查向导与冲突保护导入

**What to build:** 对通过沙箱预检的合法 `.ummpk` 模组包，启动器弹出显式审查向导对话框。向导展示该包的名称、作者、版本、说明以及将要导入的插件和配置文件列表；默认目标为当前活跃 Profile，并允许用户下拉切换已有 Profile 或选择“以此包新建独立方案”；对比目标 Profile 中的文件，醒目标注版本变更与同名冲突；同名 `.dll` 覆盖前自动备份为 `.bak`，配置文件（`.cfg`）默认勾选“保留本地已有配置”；只有用户在向导中显式点击“确认导入”后才执行物理写入，并自动刷新模组管理视图。

**Blocked by:** 03: 沙箱违规拦截红牌告警与安全预检

**Status:** resolved

- [x] 实现 `.ummpk` 导入审查向导窗口/视图模型，展示元数据与拟写入文件清单
- [x] 支持目标 Profile 切换（当前活跃 / 其他已有方案 / 以包名新建方案）
- [x] 冲突计算引擎：标出同名插件覆盖与配置重叠
- [x] 冲突保护机制：被覆盖的 `.dll` 自动生成 `.bak` 备份，`.cfg` 默认保留本地配置
- [x] 用户确认后原子解压写入物理目录，并刷新模组列表与活跃方案状态
- [x] 包含冲突计算、`.bak` 备份命名与配置保留分支的单元测试

## Answer
已通过 TDD 循环完成 Ticket 04 的全部交付：
1. 实现了 `ModPackageImportPlan`、`ModPackagePlanEntry`、`ModPackageEntryConflictType` 与 `ModPackageImportOptions` 数据结构；
2. 实现了冲突计算引擎 `PluginProfileService.InspectPackagePlan`，精确对比本地文件系统计算同名插件覆盖（`ConflictDll`）与配置文件冲突（`ConflictConfig`）；
3. 实现了冲突保护与安全解压引擎 `PluginProfileService.ImportPackageWithOptions`，同名 `.dll` 覆盖前自动备份为 `.bak`，`.cfg` 默认安全保留本地玩家个性化配置，支持目标 Profile 切换或使用包名新建独立方案；
4. 构建了专用的模组包审查向导窗口 `ModPackageImportWindow`（WPF-UI 规范设计），清晰呈现元数据、Profile 目标下拉框、冲突保护策略选项卡片与文件清单徽章，只有显式点击“确认导入”才执行物理写入；
5. 在 `MainWindow.HandleShellFileIntent` 中接入了模组包向导流转与 `ModListPage` 外部导入后的实时自动刷新；
6. 在 `ModPackageImportTests.cs` 中增加了冲突识别、`.bak` 备份生成、配置文件保留与覆盖分支的单元测试，全套测试 109/109 全部绿灯通过。
