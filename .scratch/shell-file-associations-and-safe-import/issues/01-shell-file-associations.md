# 01: Windows 文件类型注册与设置页管理

**What to build:** 在非便携模式下，应用启动时自动在 Windows 注册表中维护 `.ummpk` 与 `.ummtheme` 的文件扩展名关联、程序标识（ProgID）、图标与打开命令行。在“设置”页面的高级/系统设置区域提供“修复文件关联”与“解除文件关联”按钮，方便用户随时查看状态并一键手动恢复或清除注册表关联。若处于便携隔离环境（`AppDataPaths.IsIsolatedProfile`）或非 Windows 系统，所有注册表写入操作均静默跳过。

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] 在 `HKCU\Software\Classes` 下为 `.ummpk` 和 `.ummtheme` 生成合法的打开命令注册（指向当前 exe 路径与 `%1`）
- [x] 提供解除文件关联（清理注册表中相关项）的可靠实现
- [x] 在设置页面提供“修复关联”与“解除关联”的交互按钮，并显示当前关联状态提示
- [x] 严格防呆：便携隔离模式下不写入任何注册表项
- [x] 包含针对文件关联与注册键生成的单元测试（验证路径转义、便携模式跳过逻辑）

## Answer
已通过 TDD 测试先行完成 Ticket 01 的全部交付：
1. 实现了 `ShellAssociationService`，遵循 ADR-0004 规范在 `HKCU\Software\Classes` 注册与自愈 `.ummpk` 和 `.ummtheme` 打开方式，并在便携隔离模式下安全静默跳过；
2. 在 `SettingsViewModel` 与 `SettingsPage.xaml` 中添加了 Windows 文件关联卡片，支持实时查看关联状态以及一键“修复/关联”与“解除关联”；
3. 新增 `UnturnedModManager.Tests/ShellAssociationTests.cs`，单元测试 96/96 全部通过。
