# v2.1.6 发布执行报告

## 需求执行概述

将已审核的 UMM v2.1.6 发布到 GitHub Release，并让 Release 正文同时提供启动器欢迎区可提取的简要更新内容与完整更新说明。

## 发布内容溯源

| 需求 | 证据 |
| --- | --- |
| 主页动态读取 Release 摘要 | `Services/LauncherUpdateService.cs`、`ViewModels/HomeViewModel.cs`，源码修复提交 `5b5065b` |
| 欢迎区可读摘要 | Release 正文顶部 `## 欢迎区简要更新内容` 的 4 条 `- ` 项目符号 |
| 完整更新内容 | 同一 Release 正文的 `## 详细更新内容`、安全边界、验证和下载小节 |
| 版本与仓库更新日志 | `CHANGELOG.md` 的 v2.1.6 条目 |
| Windows 发布资产 | `UnturnedModManager-v2.1.6-win-x64.exe` |

## 构建与资产验证

- 发布命令：`dotnet publish .\UnturnedModManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false`
- 发布包：单目录仅 1 个文件，`UnturnedModManager.exe`。
- 文件版本：`2.1.6.0`。
- 资产发布名：`UnturnedModManager-v2.1.6-win-x64.exe`。
- 大小：`169,533,376` 字节。
- SHA-256：`A8A498E25A9ADFB3978F7BB87FACC8E650BA11BEFCF44B937BE2D97240F114FD`。
- 测试：`dotnet test .\UnturnedModManager.Tests\UnturnedModManager.Tests.csproj -c Release --no-restore`，`40/40` 通过。

## 独立审核记录

- **判定**：PASS。
- **审核范围**：单 EXE 完整性、版本和资产命名、首页 Markdown 摘要解析、完整 Release 正文、安全声明、更新下载校验以及 v2.1.5 不被改写。
- **阻断项**：无。
- **确认保留的安全边界**：仅远端版本高于本地时提供下载；资产必须来自官方 HTTPS Release 地址，且校验名称、大小与 GitHub API SHA-256 摘要；下载和安装需用户确认。

## 远端发布验证

- Release URL：`https://github.com/YU80Rice/UnturnedModManager/releases/tag/v2.1.6`
- `GET /releases/latest` 已返回 `v2.1.6`，因此它已成为 Latest。
- GitHub 返回的唯一资产名、大小和 `sha256:a8a498e25a9adfb3978f7bb87facc8e650ba11befcf44b937be2d97240f114fd` 与本地完全匹配。
- Release 正文的首 4 条项目符号可被 `ExtractAnnouncementHighlights` 提取，且 `## 详细更新内容` 小节存在。
- `v2.1.5` Release 仍存在且仍保有原有单个资产，未被替换或改写。

## Git 传输说明

普通 Git HTTPS 推送因 `github.com:443` 连接超时未完成。GitHub API 可用，因此通过官方 Git Data API 以相同父提交、相同树和相同提交信息更新远端 `main` 并创建注释标签。

- 本地文档提交：`d187f7501092df8b431f88b5fd3c71151821acce`。
- GitHub API 对提交者时区做规范化，远端等价提交为：`dfe70077733d025b4eb4f926da636cd12135e539`。
- 两者对应的树均为：`0400e168d385197a6a7a03309c5910fb2ebed333`；因此仓库内容一致。
- 远端 `v2.1.6` 标签指向该远端等价提交。

## 最终结论

v2.1.6 已公开发布为 Latest，单文件 EXE、摘要正文、详细更新说明、仓库更新日志和远端校验均已完成。可移交用户下载验证。
