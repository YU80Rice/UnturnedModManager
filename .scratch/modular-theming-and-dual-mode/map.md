# Map: 深度模块化主题系统与日夜双态生态 (Wayfinder Map)

## 目的地

实现 Unturned Mod Manager 深度模块化主题系统 2.0。建立严格的设计代币体系（Design Tokens）并打通 WPF-UI 内部底层色彩总线（`ApplicationAccentColorManager`），根除所有硬编码及默认系统蓝残留；升级 `.ummtheme v2` 规范以支持显式日夜双态（Light & Dark 双色板与双壁纸）及旧版 v1 智能派生向下兼容；在全软件核心视窗与导入向导中落地 WCAG 2.1 AA 级对比度守卫与日夜联动预览。

## 笔记

- 领域：WPF-UI 3.0.5 主题总线、XAML 设计代币重构、日夜双态自适应算法、WCAG 2.1 AA 规范
- 查阅技能：`/domain-modeling`、`/codebase-design`、`/tdd`、`/implement`
- 固定偏好：
  - 核心设计代币优先：色彩 + 壁纸 + 卡片圆角/不透明度，字体定制延后至下一阶段；
  - 旧版 v1 主题运行时自动无缝升权；
  - 严格保持 0 警告 0 错误与 100% 自动化测试覆盖。

## 已有决策

- [01: WPF-UI 内部强调色总线打通与默认蓝底根治](issues/01-wpf-ui-accent-manager-integration.md) — 接入 ApplicationAccentColorManager 并统一语义代币，消灭主页与关于页残留默认蓝
- [02: .ummtheme v2 日夜双态数据模型与 v1 智能升级派生](issues/02-dual-mode-theme-schema-and-backward-compatibility.md) — 确立 v2 双模规范与双壁纸支持，实现 EnsureDualMode 浅色自动派生算法
- [03: 运行时日夜模式联动与全局主题自适应切换](issues/03-theme-service-day-night-runtime-switching.md) — 重构 ThemeService 支持自定义主题白天/夜间态热翻转、双壁纸调度与持久化恢复

## 尚未明确

- 社区工坊的主题热插拔与在线主题广场协议扩展（待本地主题系统稳定后探索）
- 自定义字体族（FontFamily）与窗口磨砂特效（Mica/Acrylic）的高级代币开放

## 范围外

- 运行时实时交互式主题调色编辑器（本期专注于主题包规范与导入换肤，非在线调色器）
- 引入重量级第三方主题引擎或修改 WPF 核心渲染管道
