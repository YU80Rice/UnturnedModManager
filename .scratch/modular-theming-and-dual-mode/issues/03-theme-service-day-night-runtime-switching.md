# 03: 运行时日夜模式联动与全局主题自适应切换

Type: task
Status: ready-for-agent
Blocked by: 01, 02

## 问题

当前 `ThemeService` 当用户点击左下角“深色/浅色模式”按钮时，直接调用 `Apply(ThemePreference)` 并回退至默认内置调色板，导致当前激活的自定义主题失效丢失；且无法在自定义主题内部丝滑切换白天（Light）与夜间（Dark）双重形态。

如何重构 `ThemeService` 运行时换肤流，使其在自定义主题激活状态下，点击日夜按钮时无缝在当前主题的“白天版”与“夜间版”之间热切换？

## 验收条件

- [ ] 重构 `ThemeService.Apply` 与模式切换处理：若当前存在活跃 `CurrentCustomTheme`，点击日夜模式时切换该主题的对应态（白天/夜间）而非卸载主题
- [ ] 动态更新背景壁纸：白天模式加载 `LightBackgroundAsset`，夜间模式加载 `BackgroundAsset`，平滑过渡
- [ ] 动态注入对应模式的文字高对比度笔刷与卡片透明度
- [ ] 将日夜模式选择与当前自定义主题绑定持久化到 `settings.json`，重启后无缝恢复对应的白天/夜间状态
- [ ] 编写测试验证：激活自定义主题后切换为 Light/Dark 模式，主题保持活跃且背景色、卡片色与壁纸正确翻转
