# 01: WPF-UI 内部强调色总线打通与默认蓝底根治

Type: task
Status: ready-for-agent

## 问题

当前换肤后，主页“欢迎区”徽章、v2.2.0 标签、更新要点小圆点、运行日志图标、关于页魔方 Logo 底色、版本号药丸、GitHub 链接及侧边栏头像圆底依然保留为 WPF-UI 默认的系统蓝（#0078D4）。这是由于 `ThemeService` 仅向外部 ResourceDictionary 注入了部分样式，未触发 WPF-UI 核心的 `ApplicationAccentColorManager.Apply`，导致其内部 `ThemesDictionary` 未能感知主题色变化。

如何打通 WPF-UI 3.0.5 内部强调色总线，重构 XAML 视图层建立统一的语义设计代币（Semantic Tokens），彻底消除所有硬编码与孤岛蓝底？

## 验收条件

- [ ] 在 `ThemeService.ApplyCustomTheme` 与 `ApplyPalette` 中接入 `ApplicationAccentColorManager.Apply(accent, applicationTheme, updateAccentControlResources: true)`
- [ ] 确保 `Application.Current.Resources` 内部的 `AccentFillColorDefaultBrush`、`AccentFillColorSecondaryBrush`、`AccentFillColorTertiaryBrush` 均与当前主题严格同步
- [ ] 重构 `HomePage.xaml`、`AboutPage.xaml`、`MainWindow.xaml`，消除对硬编码或隐式系统蓝的依赖，统一使用高内聚语义代币
- [ ] 切换为粉色或橙色主题时，主页徽章、标签、圆点、魔方 Logo 底色、头像圆底 100% 同步为对应主题色
- [ ] 编写测试断言 `ThemeService` 换肤后 WPF-UI 内部资源与 `Application.Current.Resources` 的强调色正确更新
