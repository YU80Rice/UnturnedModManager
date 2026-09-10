# UMM 最新仓库快照研究（2026-09-10）

> 研究范围：只读核对 `UnturnedModManager` 当前本地仓库、项目文档与 GitHub 一手 API。报告明确区分已验证事实、静态/本地证据、运行时未验证与外部阻塞。研究日期：2026-09-10（Asia/Shanghai）。

## 1. 结论摘要

当前工作区已经推进到 `v2.2.1` 之后的 Wayfinder 快照起点：本地 `main` 与 `origin/main` 均指向 `f0cae2f6a46db6cde80a28b17360545977c2c3b3`，工作树干净；最新提交同步了 README、BLUEPRINT 与 CONTEXT 的 v2.2.1 里程碑文本。`v2.2.1` 已是公开的非草稿、非预发布 GitHub Release，并且公开资产已经存在，旧 handover 中“v2.1.8 EXE 资产尚未上传”的历史阻塞已不再适用于当前公开状态。

当前明确里程碑是：Phase 1–5 已在 v2.2.0 验收，Phase 6（玩家自定义壁纸、高斯模糊、遮罩、滚轮路由）已在 v2.2.1 验收；HEAD 的快照标签为 `snapshot/wayfinder-unturned-native-hud-start`。当前没有可从本地文档确认的“原生 HUD”实现验收结论，因此该标签应视为下一阶段起点，而不是功能完成证明。

## 2. Git 本地快照与远端一致性

### 已验证事实（本地 Git）

| 项目 | 结果 |
| --- | --- |
| 分支 | `main` |
| HEAD | `f0cae2f6a46db6cde80a28b17360545977c2c3b3` |
| `origin/main` | `f0cae2f6a46db6cde80a28b17360545977c2c3b3` |
| 工作树 | `git status --porcelain=v1` 无输出，当前快照干净 |
| 远端 URL | `https://github.com/YU80Rice/UnturnedModManager.git` |
| `git ls-remote` | 远端 `refs/heads/main` 与本地 HEAD 一致；`v2.2.0` 与 `v2.2.1` 标签可见 |
| 描述 | `snapshot/wayfinder-unturned-native-hud-start` |
| 子模块 | 当前仓库未显示递归子模块状态输出；不能据此推断外部 U3-SDK 或 UML 仓库完整性 |

### 近期提交（本地 log）

1. `f0cae2f`（2026-09-03 19:39 +08:00）：同步 README、BLUEPRINT、CONTEXT 至 v2.2.1 里程碑。
2. `ce30667`（2026-09-03 19:28）：版本升级到 2.2.1 并生成发布包。
3. `84824ca`（2026-09-03 19:19）：将主页鼠标滚轮事件路由至 `RootScrollViewer`。
4. `d06bd5a`（2026-09-03 19:01）：完成壁纸设置控件、实时滑块与端到端测试。
5. `f208f5b`（2026-09-03 18:09）：完成主窗口毛玻璃宿主与二级页面透明化。
6. `d6b76e6`（2026-09-03 17:52）：完成个性化持久化与非阻塞图片加载。

## 3. 版本、项目与本地发布目录

### 已验证事实（源码与文件系统）

- `UnturnedModManager.csproj` 声明 `<Version>2.2.1</Version>`、`<AssemblyVersion>2.2.1.0</AssemblyVersion>`、`<FileVersion>2.2.1.0</FileVersion>`、`RuntimeIdentifier=win-x64`。
- 技术栈为 .NET 8 WPF，引用 `WPF-UI` 3.0.5 与 `System.Management` 8.0.0。
- `publish/` 已有 `UMM-v2.1.5-win-x64` 至 `UMM-v2.2.1-win-x64` 目录；v2.2.0 与 v2.2.1 目录均含 `UnturnedModManager.exe`。
- 本地 v2.2.1 EXE 大小为 76,029,280 bytes，SHA-256 为 `FAD12339E355D01D28AA5A2B4DA1F83E1DF7F40A288289AA836ECB569414F37B`；该文件时间为 2026-09-03 19:28:02（本地时间）。
- v2.2.1 发布目录还包含 PDB、ZIP 与多个诊断包/日志；这些是本地快照中的文件事实，不等于对外发布资产或最新运行验收证据。

## 4. 当前产品里程碑与文档待办

### README / BLUEPRINT / CONTEXT 的当前静态结论

- README 标题为 `Unturned Mod Manager v2.2.1`，把 v2.2.1 定位为 Windows 启动、BepInEx 插件管理与社区客户端。
- BLUEPRINT 里 Phase 4（开放式主题框架）与 Phase 5（Windows 原生关联）标记为 v2.2.0 已验收；Phase 6（玩家自定义壁纸与高斯模糊个性化）标记为 v2.2.1 已验收。
- README 与 TECHNICAL_HANDOVER 均强调：构建/单元测试不是完整验收；真实 UI、社区网络、BepInEx 安装/启停/卸载、插件回滚、拖放、协议与实际 Unturned 启动仍需隔离数据目录下验证。
- CONTEXT 保留的关键安全边界是：本地 ZIP 导入限制在 `BepInEx/plugins/**` 与 `BepInEx/config/**`；社区安装器仍存在目录白名单不对等的高优先级安全债务。
- TECHNICAL_HANDOVER 的发布日期说明仍写“截至 2026-08-22”，并保留 v2.1.8 时代的发布说明；它是历史交接资料，不能直接覆盖 v2.2.1 公开 API 的现状。

### 明确待办（来自当前文档）

1. 为 `CommunityModInstaller` 补齐与本地 ZIP 导入器等价的 `BepInEx/plugins` / `BepInEx/config` 白名单、DLL 存在约束和回归测试。
2. 建立隔离 `UMM_DATA_DIRECTORY` 的真实 UI 冒烟矩阵，覆盖主题、配色、窗口尺寸、侧栏、滚动与返回上下文。
3. 对社区登录回调、身份显示、GitHub Release 下载回退、BepInEx 多来源、插件更新/卸载回滚做可重复网络验收。
4. 继续补充可访问性、键盘导航、错误恢复及“跟随系统”运行时变化响应。
5. 对 Wayfinder/native HUD 下一阶段，先补齐需求/源码位置/验收标准，再单独形成运行时证据；当前标签本身不是 PASS。

## 5. 构建、测试与 CI 入口

### 本地/仓库静态事实

- `README.md` 与 `TECHNICAL_HANDOVER.md` 给出的常用门禁为：`dotnet build -c Release`、`dotnet test .\\UnturnedModManager.Tests\\UnturnedModManager.Tests.csproj`、win-x64 self-contained `dotnet publish`、`git diff --check` 与 `git status --short`。
- `.github/workflows/ci.yml` 在 Windows runner 上使用 .NET 8，执行测试项目 restore、Release build 与 test；触发条件为 `main` push 和针对 `main` 的 pull request。
- `.github/workflows/release-asset.yml` 是手动 workflow：输入已有 tag，按 win-x64 self-contained single-file 发布，并用 `gh release upload` 上传到既有 Release。

### 运行时未验证

本次是只读快照研究，没有执行 build、test、publish、真实 WPF UI、社区网络、BepInEx 或 Unturned 启动验证。因此不能把本地发布目录中的 EXE、历史诊断包或公开 CI 成功记录转述为当前源码快照的完整运行时 PASS。

## 6. GitHub 一手 API 核对（2026-09-10）

查询接口：

- `https://api.github.com/repos/YU80Rice/UnturnedModManager`
- `https://api.github.com/repos/YU80Rice/UnturnedModManager/releases`
- `https://api.github.com/repos/YU80Rice/UnturnedModManager/actions/runs?per_page=10`
- `https://api.github.com/repos/YU80Rice/UnturnedModManager/issues?state=open&per_page=100`

### 仓库与 Release

- 仓库公开、未归档，默认分支为 `main`；API 的 `pushed_at` 为 `2026-09-03T12:18:42Z`，`open_issues_count` 为 4。
- 最新公开 Release 为 `v2.2.1`，非 draft、非 prerelease，发布时间 `2026-09-03T12:21:59Z`。
- v2.2.1 公开资产已存在：`UMM-v2.2.1-win-x64.zip`（70,189,498 bytes，下载 17 次）、`UnturnedModManager.exe`（76,029,280 bytes，下载 10 次）、`UnturnedModManager.pdb`（193,596 bytes，下载 0 次）。
- `v2.2.0` 也有 ZIP/EXE 资产；`v2.1.8` 当前公开资产名为 `UnturnedModManager-v2.1.8-win-x64.exe`，大小 75,934,560 bytes，下载 16 次。由此可确认历史 v2.1.8 资产缺失阻塞已被后续公开状态解决。

### GitHub Actions

公开 API 返回最近一次 `build-and-test` 为 run 17：`main` push，HEAD `f0cae2f6...`，状态 `completed`、结论 `success`，创建时间 `2026-09-03T12:18:44Z`，完成时间 `2026-09-03T12:19:49Z`。

最近 10 次公开 workflow run 均为 `build-and-test`，所列结论均为 `success`；该事实证明对应 CI run 成功，不证明本地工作树在 2026-09-10 重新构建或完成真实游戏验收。

### 当前公开 Open Issues

API 返回 4 个 open issues（排除了 pull request）：

| Issue | 标题 | 最近更新时间 | 类型 |
| --- | --- | --- | --- |
| #9 | `[Bug] 模组管理器下载不了mod` | 2026-08-23 | bug |
| #6 | `[Bug]` | 2026-07-15 | bug |
| #3 | `[Bug] 启动失败` | 2026-07-12 | bug |
| #1 | `老铁 你项目忘记开讨论了` | 2026-07-11 | 无标签 |

这些 Issue 仍是外部待处理事项；本次没有读取每个 Issue 的完整正文、附件或复现环境，因此不能推断其根因、修复状态或与 v2.2.1 的对应关系。

## 7. 证据分层与外部阻塞

### 已验证事实

- 本地 Git HEAD、分支、工作树、远端 ref、近期提交与标签。
- 项目版本字段、CI/release workflow 文件、README/BLUEPRINT/CONTEXT/TECHNICAL_HANDOVER 的文本。
- 本地 v2.2.1 发布目录及 EXE 的大小/哈希。
- GitHub 公开仓库、Release、Actions runs 与 open issues API 返回值（查询日期 2026-09-10）。

### 仅静态/本地证据

- Phase 4–6 的“已验收”是 BLUEPRINT 文档标记与已有提交/测试文件的静态状态；本次没有重新执行验收。
- 本地诊断包和日志只能证明工作区保存过这些文件，不能自动证明它们对应当前 HEAD、当前 EXE 或完整三环境验收。
- TECHNICAL_HANDOVER 中关于 v2.1.8 的状态是历史基线，需让位于最新 GitHub API 事实。

### 运行时未验证

- 当前 HEAD 的 WPF UI、主题/壁纸交互、滚轮路由、社区登录/下载、BepInEx 安装与卸载、插件更新回滚、协议入口、实际 Unturned 启动。
- Wayfinder/native HUD 标签对应功能的实现与验收。
- 当前本地 v2.2.1 EXE 是否可在目标 Windows 环境稳定运行。

### 外部阻塞/风险

- 仍有 4 个公开 open issues，至少包含下载 mod、启动失败等未关闭用户问题。
- 社区安装器目录白名单不对等的安全债务仍由当前文档明确记录，需在扩展社区安装能力前处理。
- 发布资产已公开，但本地发布目录混有诊断包与日志；后续发布/归档应继续区分可交付资产、运行证据和临时诊断材料。

## 8. 建议的下一步

1. 以 `f0cae2f6...` + `snapshot/wayfinder-unturned-native-hud-start` 作为下一阶段冻结基线。
2. 先把 native HUD 需求、源码入口、平台边界和验收矩阵写成独立 spec，再实施代码。
3. 处理 `CommunityModInstaller` 白名单债务，并为 #9/#6/#3 收集版本、日志、复现步骤和目标环境后再判定修复优先级。
4. 对 v2.2.1 重新执行一次隔离数据目录下的 UI/网络/游戏启动验收，并把证据绑定到具体 commit 与 EXE SHA-256。
5. 更新 `TECHNICAL_HANDOVER.md` 的版本日期和 v2.1.8 历史发布段落，避免新 Agent 把旧发布阻塞误认为当前状态。

## 9. 本地证据路径

- `README.md`
- `BLUEPRINT.md`
- `CONTEXT.md`
- `TECHNICAL_HANDOVER.md`
- `.github/workflows/ci.yml`
- `.github/workflows/release-asset.yml`
- `UnturnedModManager.csproj`
- `publish/UMM-v2.2.1-win-x64/UnturnedModManager.exe`

