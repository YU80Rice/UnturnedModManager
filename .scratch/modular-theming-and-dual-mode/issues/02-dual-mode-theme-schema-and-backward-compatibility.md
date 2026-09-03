# 02: .ummtheme v2 日夜双态数据模型与 v1 智能升级派生

Type: task
Status: ready-for-agent

## 问题

现有 `.ummtheme` 仅支持单套暗色（`BaseTheme: Dark`）配置，缺少白天（Light）模式的专属调色板与壁纸定义。当用户处于白天模式时无法获得和谐的浅色视觉体验；且需无缝兼容现有单模态 v1 主题包，避免造成生态割裂。

如何设计 `.ummtheme v2` 双态数据模型，并实现针对旧版 v1 主题的自动升权与浅色算法派生引擎？

## 验收条件

- [ ] 扩展 `CustomTheme` 数据模型，支持显式日间配置属性：`LightBackgroundColor`、`LightCardBackgroundColor`、`LightBackgroundAsset` 等
- [ ] 实现针对 v1 单模态主题的智能升级器：若主题缺少日间配置，算法自动基于主强调色推导出温润柔和、高对比度的浅色白天代币（如粉色主题派生为柔白粉底 #FFF5F8、浅粉卡片 #FFEBF2 与暗色清晰文本）
- [ ] 扩展 `ThemePackageService`：支持打包与导出包含双壁纸（`wallpaper_dark.png` 与 `wallpaper_light.png`）及双态配置的 `.ummtheme v2` 标准包
- [ ] 安全沙箱支持验证并放行双壁纸资源（确保依然拦截恶意脚本与越界路径）
- [ ] 编写单元测试覆盖：v2 双模包解析、双壁纸提取、v1 旧包自动算法派生为双模对象
