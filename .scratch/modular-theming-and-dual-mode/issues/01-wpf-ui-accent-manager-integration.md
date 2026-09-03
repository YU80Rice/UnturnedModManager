# 01: WPF-UI 内部强调色总线打通与默认蓝底根治

Type: task
Status: resolved

## 问题

当前换肤后，主页“欢迎区”徽章、v2.2.0 标签、更新要点小圆点、运行日志图标、关于页魔方 Logo 底色、版本号药丸、GitHub 链接及侧边栏头像圆底依然保留为 WPF-UI 默认的系统蓝（#0078D4）。这是由于 `ThemeService` 仅向外部 ResourceDictionary 注入了部分样式，未触发 WPF-UI 核心的 `ApplicationAccentColorManager.Apply`，导致其内部 `ThemesDictionary` 未能感知主题色变化。

如何打通 WPF-UI 3.0.5 内部强调色总线，重构 XAML 视图层建立统一的语义设计代币（Semantic Tokens），彻底消除所有硬编码与孤岛蓝底？

## 验收条件

- [x] 在 `ThemeService.ApplyCustomTheme` 与 `ApplyPalette` 中接入 `ApplicationAccentColorManager.Apply(accent, accent, pointerOver, pressed)`
- [x] 确保 `Application.Current.Resources` 内部的 `AccentFillColorDefaultBrush`、`AccentFillColorSecondaryBrush`、`AccentFillColorTertiaryBrush` 均与当前主题严格同步
- [x] 重构 `HomePage.xaml`、`AboutPage.xaml`、`MainWindow.xaml`，消除对硬编码或隐式系统蓝的依赖，统一使用高内聚语义代币
- [x] 切换为粉色或橙色主题时，主页徽章、标签、圆点、魔方 Logo 底色、头像圆底 100% 同步为对应主题色
- [x] 编写测试断言 `ThemeService` 换肤后 WPF-UI 内部资源与 `Application.Current.Resources` 的强调色正确更新

## 答案

1. 在 `ThemeService.ApplyPalette` 与 `ApplyCustomTheme` 中调用 `ApplicationAccentColorManager.Apply(accent, accent, pointerOver, pressed)`，使 WPF-UI 核心的 `ThemesDictionary` 内部强调色画刷与当前调色板主色保持绝对一致；
2. 并在每次注入时直接更新 `Application.Current.Resources["AccentFillColorDefaultBrush"]` 与 `SystemAccentColorPrimaryBrush`，并将调色板字典重排至 `MergedDictionaries` 尾部赋予最高查找优先级；
3. 将 `HomePage.xaml`（欢迎徽章、版本徽标）、`AboutPage.xaml`（魔方 Logo 前景、版本号横条文本）与 `MainWindow.xaml`（侧栏头像文字）的前景全量绑定至 `{DynamicResource TextOnAccentFillColorPrimaryBrush}`，消灭任何孤岛系统蓝；
4. 编写 `ThemeAccentManagerTests.cs` 覆盖自定义粉色主题与内置吉祥柑橙调色板的主题总线同步测试，全部绿灯通过。
