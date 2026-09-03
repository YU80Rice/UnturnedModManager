# 02: MainWindow 单层高斯模糊宿主与二级页面透明化

Type: task
Status: resolved
Blocked by: 01

## 问题

当前主窗口及所有 8 个二级页面根节点均各自硬编码了 `Background="{DynamicResource ApplicationBackgroundBrush}"`，导致多层页面重复绘制（Overdraw）且无法让统一的毛玻璃壁纸在各页面底层自然透出；同时缺少 GPU 硬件加速的 `BlurEffect` 高斯模糊层与对比度遮罩层。

如何重构 `MainWindow.xaml` 搭建高效分层的单层毛玻璃壁纸宿主，并将各页面背景透明化？

## 验收条件

- [x] 在 `MainWindow.xaml` 根节点建立三层底座架构：纯色底座 -> 单层 `Image` 壁纸宿主（附加 `BlurEffect RenderingBias="Performance"`） -> 动态遮罩层（结合日夜态自适应纯色半透明遮罩） -> 透明 UI 内容层
- [x] 重构所有二级页面（设置、插件列表、关于、首页、社区等），根背景设为 `Transparent`，让底层毛玻璃壁纸柔和透出，根除多层重复重绘与切页画面跳动
- [x] 当玩家未配置壁纸或图片不存在时，壁纸宿主层平滑隐藏折叠（`Visibility.Collapsed`），无缝呈现原生 Fluent 纯色背景
- [x] 编写测试验证：主窗口宿主在配置壁纸路径、调整模糊度、调整遮罩及清空壁纸时的视觉属性响应与折叠逻辑
