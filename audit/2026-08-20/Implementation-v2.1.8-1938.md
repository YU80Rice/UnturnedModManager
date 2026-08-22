# 实施与发布报告 - v2.1.8

## 需求执行概述

用户验收通过后，将 UMM v2.1.8 的主题可读性修复推送至 GitHub。

## 源码与发布溯源

- 提交：`cf33c960dd25c69fb6835a47063151dbb5b74049`
- 提交说明：`fix(v2.1.8): enforce theme text contrast`
- 标签：`v2.1.8`（annotated tag `29d25f4d2124e0cbbf90e9e5032b476b46cf2c35`）
- 远端分支：`origin/main` 已指向 `cf33c960dd25c69fb6835a47063151dbb5b74049`。
- 发布候选：`publish/UMM-v2.1.8-win-x64/UnturnedModManager.exe`
- 文件版本：`2.1.8.0`
- 产品版本：`2.1.8+cf33c960dd25c69fb6835a47063151dbb5b74049`
- SHA-256：`91F075BDAAFAD1EF2DC1CF03E8C0BFCFAF0342FC09D94155F739A9C11F8EB305`

## 验证记录

- Release 构建：0 warnings / 0 errors。
- 自动化测试：51 passed / 0 failed / 0 skipped。
- 子智能体审核：PASS。审核覆盖任务中心与主页普通文字、19 个 Primary 按钮、七套深色配色下默认/悬停/按下状态的白字对比度。

## GitHub 状态

- 源码与标签已成功推送。
- 已创建正式 Latest Release：`https://github.com/YU80Rice/UnturnedModManager/releases/tag/v2.1.8`。
- Release 使用简体中文摘要和详细说明。
- EXE 资产上传未完成：本机 curl 受 Windows 证书吊销检查阻断，.NET HTTPS 上传在 25 秒工具网络时限内未传完；未采取关闭证书验证或跳过吊销检查的方式绕过。

## 最终结论

- GitHub 源码、v2.1.8 标签和 Release 文本已发布。
- 本地已保留与推送提交绑定的最终 EXE；待可用网络上传通道恢复后，应将该 EXE 作为 `UnturnedModManager-v2.1.8-win-x64.exe` 附加到既有 v2.1.8 Release，并以本文 SHA-256 复核。
