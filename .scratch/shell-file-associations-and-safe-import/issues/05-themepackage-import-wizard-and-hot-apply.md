# 05: 主题包（.ummtheme）导入向导与即时热应用

**What to build:** 对通过沙箱预检的合法 `.ummtheme` 主题包，启动器弹出主题导入向导。向导展示主题名称、作者、配色板主要色彩（Primary Accent、Background Tint 等）以及背景壁纸资产缩略图预览；界面提供“立即应用该主题”复选框（默认勾选）；用户点击“确认导入”后，将主题安全解压至本地主题目录（`%AppData%\Roaming\UnturnedModManager\themes\<themeId>\`）；若勾选了立即应用，则立即触发热切换生效并持久化到配置；若取消勾选则存入主题库并在设置页主题列表中可供随时选用。

**Blocked by:** 03: 沙箱违规拦截红牌告警与安全预检

**Status:** resolved

- [x] 实现 `.ummtheme` 导入审查向导，预览主题元数据、调色板与壁纸资源
- [x] 提供“立即应用该主题”复选框，默认选中
- [x] 确认后解压主题包至数据目录下的主题存储库
- [x] 立即应用逻辑：动态注入 WPF 笔刷资源热切换并更新 `settings.json`
- [x] 包含主题包元数据读取、解压写入与热应用逻辑的单元测试

## Answer
已通过 TDD 循环完成 Ticket 05 的全部交付：
1. 实现了 `ThemePackagePreview` 数据结构，并在 `ThemePackageService` 中实现 `InspectPackagePreview`，安全提取主题元数据、基底模式、强调主色、页面与卡片色板，以及内存中流式解压壁纸缩略图；
2. 构建了专用的主题包审查向导窗口 `ThemePackageImportWindow`（WPF-UI 规范设计），直观展示主题设计卡片、调色板预览色块（Hex）、卡片不透明度与圆角，并渲染壁纸预览缩略图；
3. 提供了“导入后立即应用此主题（即刻换肤）”复选框，默认处于勾选状态；
4. 用户点击“确认导入”后，将主题安全解压保存至数据目录主题库中；若勾选了立即应用，直接调用 `ThemeService.ApplyCustomTheme` 动态注入 WPF 调色板字典与壁纸资源，实时热切换界面外观并更新主题模式；
5. 在 `ThemePackageImportTests.cs` 中增加了针对预览数据提取、解压落盘与热应用的单元测试，全套测试 111/111 全部绿灯通过。
