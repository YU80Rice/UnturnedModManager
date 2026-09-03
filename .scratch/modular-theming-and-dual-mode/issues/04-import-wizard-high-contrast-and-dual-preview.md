# 04: 导入审查向导 WCAG 对比度达标与日夜交互预览

Type: task
Status: ready-for-agent
Blocked by: 02

## 问题

当前 `ThemePackageImportWindow.xaml` 中，小节标题（“调色板与外观设计”、“背景壁纸资源”）与色块说明标签（“强调主色”、“页面背景色”等）未显式设置 `Foreground`，在特定深色容器中退化为 Windows 默认黑字（#000000），导致严重对比度不足（黑字暗底）；同时向导目前仅支持预览夜间单态，缺少日夜交互预览能力。

如何重构向导界面，彻底消除未样式化文本，实现高对比度（WCAG AA）排版，并新增日夜双模交互预览开关？

## 验收条件

- [ ] 重构 `ThemePackageImportWindow.xaml` 与 `ModPackageImportWindow.xaml` 中所有标题与标签，显式绑定 `{DynamicResource TextFillColorPrimaryBrush}` 与 `{DynamicResource TextFillColorSecondaryBrush}`
- [ ] 向导中所有展示文本在深色与浅色模式下对比度均严格符合 WCAG AA（正文 ≥ 4.5:1，大标题 ≥ 3:1）
- [ ] 在主题审查向导中新增“日间预览 / 夜间预览”联动切换控件，默认展示用户当前系统模式
- [ ] 切换预览模式时，色板色块、Hex 编码、卡片说明及壁纸缩略图即时动态切换为对应形态
- [ ] 编写测试覆盖双模预览状态机与高对比度文字样式绑定
