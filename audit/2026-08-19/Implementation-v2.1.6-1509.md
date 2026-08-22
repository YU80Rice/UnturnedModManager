# 实施执行报告 - v2.1.6

## 需求执行概述

将诊断包输出从桌面迁移至启动器目录，并实现可点击通知打开导出目录；将本地插件导入扩展为启动器任意常规页面均可拖放 `.dll` 或经校验的 BepInEx ZIP 插件包自动安装。

## 源码溯源清单

| 需求 | 落实位置 | 说明 |
| --- | --- | --- |
| 诊断包位于启动器同目录 | `Services/DiagnosticService.cs` `ExportLogs` | 使用 `AppContext.BaseDirectory`；目录名为 `UMM-诊断包_yyyyMMdd_HHmmss`。 |
| 诊断成功通知显示路径且可点击打开 | `ViewModels/HomeViewModel.cs` `ExportLogsAsync`；`Services/UserNotificationService.cs`；`MainWindow.xaml/.cs` | 通知携带完整路径和打开操作，主窗口在 UI 线程执行操作及错误提示。 |
| 任意页面拖放导入 | `MainWindow.xaml/.cs` | `FluentWindow` 和 `NavigationView` 均启用 `AllowDrop`，通过 Preview 路由集中处理并串行化导入。 |
| 本地插件页避免重复处理 | `Pages/ModListPage.xaml/.cs`；`ViewModels/LocalModsViewModel.cs` | 删除页面级 Drop 入口；导入完成时仅在当前本地插件页刷新。 |
| DLL 与 BepInEx ZIP 安全导入 | `Services/LocalModService.cs` | 仅允许 `BepInEx/plugins`、`BepInEx/config`；要求至少一个 `plugins/*.dll`；拒绝路径越界、多根、重复目标、超过 4096 条目或 1 GB 的包。 |
| 解压安全与失败一致性 | `Services/LocalModService.cs` | 元数据预检使用无溢出比较，实际流按压缩包共享剩余预算逐块限额；提交前暂存，提交失败恢复已覆盖文件并清理暂存目录。 |
| 回归验证 | `UnturnedModManager.Tests/ModelBehaviorTests.cs`；`AssemblyInfo.cs` | 覆盖诊断目录、DLL、合法包装 ZIP、非法 ZIP、实际流超预算与通知动作。 |

## 代码变更清单

- 修改：`AssemblyInfo.cs`
- 修改：`MainWindow.xaml`、`MainWindow.xaml.cs`
- 修改：`Pages/ModListPage.xaml`、`Pages/ModListPage.xaml.cs`
- 修改：`Services/DiagnosticService.cs`、`Services/LocalModService.cs`、`Services/UserNotificationService.cs`
- 修改：`ViewModels/HomeViewModel.cs`、`ViewModels/LocalModsViewModel.cs`
- 修改：`UnturnedModManager.Tests/ModelBehaviorTests.cs`

## 编译与测试验证记录

| 项目 | 命令 | 结果 |
| --- | --- | --- |
| Release 构建 | `dotnet build .\UnturnedModManager.csproj -c Release --no-restore` | 成功，0 errors，0 warnings。 |
| 测试 | `dotnet test .\UnturnedModManager.Tests\UnturnedModManager.Tests.csproj -c Release --no-restore` | 48 passed，0 failed，0 skipped。 |
| 差异检查 | `git diff --check` | 通过；仅有 Git 的 LF/CRLF 工作树提示。 |

运行环境 SDK 输出 `NETSDK1057` 信息提示当前安装的 SDK 为预览版；它是信息消息，构建汇总为 0 警告、0 错误。

## 子智能体独立审核记录

### 第一轮

判定：FAIL。

阻断项：ZIP 的 1 GB 限制只信任 `ZipArchiveEntry.Length`，而实际 `CopyTo` 无上限，恶意 ZIP 可通过低报元数据导致过量写入。

修复：以压缩包绝对路径维护独立剩余预算；预检改为无溢出比较；新增 `CopyToWithArchiveLimit` 对实际解压流逐块检查，在超过预算前停止写入。失败保留在暂存目录并由现有 `finally` 清理。

### 第二轮

判定：PASS。

审核确认原始需求均已落实，实际流上限阻断项关闭；WPF Preview 拖放路由、可点击诊断通知、路径白名单、目录穿越拒绝、重复目标拒绝和提交回滚均符合预期。审核者独立复跑 Release 构建为 0 warnings / 0 errors，测试为 48/48 PASS。

## 偏离与妥协说明

无偏离。ZIP 安装明确限制为插件与配置范围，不允许通过拖放包覆盖 BepInEx `core`、启动注入文件或游戏根目录文件。

## 后续人工验收建议

1. 在社区页、设置页和本地插件页分别拖入同一份合法 ZIP，确认均显示 Copy 光标、自动导入并在本地插件页刷新。
2. 拖入包含 `BepInEx/core`、路径穿越或仅配置文件的 ZIP，确认不会写入游戏目录并显示拒绝原因。
3. 从首页导出诊断包，确认目录创建在 `UnturnedModManager.exe` 同级，通知展示完整路径，点击后资源管理器打开该目录。
4. 在游戏目录存在同名插件时导入一份多文件 ZIP；人为制造目标文件占用后确认失败提示且已有文件内容保持原样。
