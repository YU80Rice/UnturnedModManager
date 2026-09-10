# Ticket 02: Unturned 原生风格 UI Shell 空间结构与设计代币原型 (Prototype Unturned Native UI Shell & Tokens)

Status: open
Type: prototype
Blocked by: 

## 问题

针对 PM 强调的“先建立可替换的 Unturned 风格 Shell，不同时重写现有业务服务，避免视觉重构与功能重构互相拖累”以及“借鉴空间结构、暗色半透明面板、左侧大按钮与背景氛围，但不做逐像素死板复制，保持可访问性”：

1. **设计代币（Design Tokens）体系构建**：
   - 提取 Unturned 原版主菜单的核心视觉变量：左侧大药丸按钮圆角、黑色轮廓描边、暗调半透明面板背景色（`#14181B` 与透明度）、经典几何图标风格、强调色高亮边框；
   - 确保暗色文字与背景在 WCAG 2.1 AA 守卫下达到 4.5:1 / 7:1 高对比度合规。
2. **轻量可替换 Shell 原型构建**：
   - 构建一个独立的 `UnturnedNativeShell` 原型容器（顶部状态栏 + 左侧大药丸导航栏 + 右侧 HUD 浮动主舞台 + 背景壁纸透出）；
   - 验证其如何作为一个可插拔的展示层，能够无缝挂载现有的 `Frame` / 页面切换逻辑，而不破坏底层任何 ViewModel 与 Service。
3. **输出要求**：
   - 建立 XAML 样式资源或原型视窗，产出原型截图/可测试组件供人工视觉审查。
