# 03: 运行时日夜模式联动与全局主题自适应切换

Type: task
Status: resolved
Blocked by: 01, 02

## 问题

当前 `ThemeService` 当用户点击左下角“深色/浅色模式”按钮时，直接调用 `Apply(ThemePreference)` 并回退至默认内置调色板，导致当前激活的自定义主题失效丢失；且无法在自定义主题内部丝滑切换白天（Light）与夜间（Dark）双重形态。

如何重构 `ThemeService` 运行时换肤流，使其在自定义主题激活状态下，点击日夜按钮时无缝在当前主题的“白天版”与“夜间版”之间热切换？

## 验收条件

- [x] 重构 `ThemeService.Apply` 与模式切换处理：若当前存在活跃 `CurrentCustomTheme`，点击日夜模式时切换该主题的对应态（白天/夜间）而非卸载主题
- [x] 动态更新背景壁纸：白天模式加载 `LightBackgroundAsset`，夜间模式加载 `BackgroundAsset`，平滑过渡
- [x] 动态注入对应模式的文字高对比度笔刷与卡片透明度
- [x] 将日夜模式选择与当前自定义主题绑定持久化到 `settings.json`，重启后无缝恢复对应的白天/夜间状态
- [x] 编写测试验证：激活自定义主题后切换为 Light/Dark 模式，主题保持活跃且背景色、卡片色与壁纸正确翻转

## 答案

1. 重构 `ThemeService.Apply` 与 `ApplyCustomTheme`，支持保留当前激活的 `CurrentCustomTheme`；当用户点击切换日夜模式时，热更新对应白天/夜间态的设计代币（背景底色、卡片背景、卡片不透明度、卡片圆角以及高对比度文字色），不再发生误卸载或回退默认调色板；
2. 调度双壁纸资源（`CustomWallpaperDarkPath` 与 `CustomWallpaperLightPath`），利用内存流加载避免文件句柄占用，白天模式自动呈现浅色壁纸，夜间模式自动呈现深色壁纸；
3. 在 `AppSettings` 中持久化 `ActiveCustomThemeId` 及双壁纸路径，在 `ThemeService.Initialize` 时自动按保存的日夜模式恢复自定义主题；
4. 编写 `ThemeRuntimeSwitchingTests.cs`（3 项测试）覆盖日夜模式无缝热翻转、双壁纸动态调度及持久化与重置，全部绿灯通过。
