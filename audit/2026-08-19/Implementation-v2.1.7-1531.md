# 实施与发布候选报告 - v2.1.7

## 需求执行概述

发布包含诊断包目录迁移、可点击通知、任意页面 DLL/BepInEx ZIP 拖放导入及 ZIP 实际解压流限额修复的 UMM v2.1.7，并同步版本信息、README、CHANGELOG 和 GitHub Release 更新说明。

## 源码溯源清单

| 需求 | 落实位置 |
| --- | --- |
| 诊断包同级输出与可点击通知 | `Services/DiagnosticService.cs`、`ViewModels/HomeViewModel.cs`、`Services/UserNotificationService.cs`、`MainWindow.xaml/.cs` |
| 全局拖放导入与当前页刷新 | `MainWindow.xaml/.cs`、`Pages/ModListPage.xaml/.cs`、`ViewModels/LocalModsViewModel.cs` |
| BepInEx ZIP 安全校验、暂存、回滚、实际流上限 | `Services/LocalModService.cs` |
| v2.1.7 版本一致性 | `UnturnedModManager.csproj`、`AppSettings.cs`、`Pages/AboutPage.xaml`、`README.md`、`CHANGELOG.md` |
| 回归测试 | `UnturnedModManager.Tests/ModelBehaviorTests.cs`、`AssemblyInfo.cs` |

## 变更与提交

- 提交：`9b6a382 release(v2.1.7): add safe launcher drop import`
- 发布版本：`v2.1.7`
- 未纳入提交：`.qa/`、`UMM-V2.1.4-Promo/`、既有 `audit/` 报告和 `publish/` 本地产物。

## 编译、测试与发布产物

| 项目 | 命令或证据 | 结果 |
| --- | --- | --- |
| Release 构建 | `dotnet build .\UnturnedModManager.csproj -c Release --no-restore` | 0 errors，0 warnings。 |
| 自动化测试 | `dotnet test .\UnturnedModManager.Tests\UnturnedModManager.Tests.csproj -c Release --no-restore` | 48 passed，0 failed，0 skipped。 |
| 单文件发布 | `dotnet publish .\UnturnedModManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --no-restore --nologo -o .\publish\UMM-v2.1.7-win-x64` | 成功。 |
| 候选资产 | `publish\UMM-v2.1.7-win-x64\UnturnedModManager.exe` | AMD64 Windows GUI，单一 EXE，75,929,952 bytes。 |
| 文件版本 | FileVersion `2.1.7.0`；ProductVersion `2.1.7+9b6a3826f37a8b73656149b1f3014c8e606b02a8` | 与提交一致。 |
| SHA-256 | `1FCFE2EA4ABB15C53EC1A33B4693C88228D07B3173EA498BE71EC656BBF391DF` | 待作为 GitHub Release 资产摘要复核。 |

SDK 输出 `NETSDK1057` 预览版提示；构建汇总为 0 警告、0 错误。发布资产仅上传 EXE，不上传本地 PDB。

## 独立审核记录

### 第一轮

判定：FAIL。

阻断项：旧 `bin\Release\net8.0-windows\win-x64\publish\UnturnedModManager.exe` 的 FileVersion 为 `2.1.4.0`，不能作为 v2.1.7 Release 资产。

### 修复与第二轮

从提交 `9b6a382` 重新执行 `win-x64` 自包含单文件发布，并验证产物 FileVersion、ProductVersion、PE Machine、Subsystem 与 SHA-256。

第二轮判定：PASS。

审核确认新资产为 AMD64 Windows GUI 单文件 EXE，版本与提交、README、About 页、CHANGELOG 和 Release 更新说明一致。v2.1.6 既有正式 Release 不会被覆盖；更新检测可识别 `v2.1.7`，且严格要求同名资产和 GitHub SHA-256 摘要。

## 发布后复核清单

1. `main` 已推送到 `1747169c5ef03d9aef59078b8c1a3880f061f590`。
2. 已创建带注释标签 `v2.1.7`；其 peeled commit 为 `1747169c5ef03d9aef59078b8c1a3880f061f590`。
3. GitHub Release 已创建为正式 Latest，`Draft=false`、`Prerelease=false`：`https://github.com/YU80Rice/UnturnedModManager/releases/tag/v2.1.7`。
4. 唯一资产为 `UnturnedModManager-v2.1.7-win-x64.exe`，大小 `75,930,976` bytes；GitHub digest 为 `sha256:0c1da5f0f5905b78183f303777db7910b247227177026e3f4efbddc517d94678`，与本地 SHA-256 一致。
5. GitHub Release 正文已复核，保持“欢迎区简要更新内容”与“详细更新内容”两层结构；首段四条要点可被启动器欢迎区读取。
