# 3. 开放式主题框架与动态无障碍对比度规范

## 上下文与问题陈述
传统启动器要么仅支持死板的黑白双色，要么在开放自定义主题时容易出现“蓝底黑字”、“灰底白字”等文字不可读、按钮对比度崩塌的问题（违反 WCAG AA 4.5:1 可访问性标准）。

## 决策
我们设计了**开放式 `.ummtheme` 主题包标准**与**全局动态资源注入流水线**，并在 [`ThemeService`](file:///D:/Agent-工作目录/DevelopMyUNMultiplayerModAndModloader/启动器/UnturnedModManager/Services/ThemeService.cs) 中实施严格的对比度计算与环境属性级联。

## 关键机制
1. **`.ummtheme` 规范与安全沙箱**：标准 ZIP 归档仅允许 `theme.json` 与背景图片资源（PNG/JPG/WEBP），解压体积限定 <= 50MB，杜绝恶意脚本注入。
2. **WCAG AA 动态前景色算法**：根据强调色与卡片表面亮度，通过相对亮度（Relative Luminance）公式自动计算并注入最佳对比度文字画刷（`TextOnAccentFillColorPrimaryBrush`），确保主要文本 $\ge 7:1$、次要与交互文本 $\ge 4.5:1$。
3. **环境属性级联取代全局 TextBlock 覆写**：通过 `Window` / `Page` 级联 `TextElement.Foreground`，保持按钮与控件内部状态机对文字前景的控制权，彻底根治禁用态与悬停态对比度失真。
