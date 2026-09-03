# 02: .ummtheme v2 日夜双态数据模型与 v1 智能升级派生

Type: task
Status: resolved

## 问题

现有 `.ummtheme` 仅支持单套暗色（`BaseTheme: Dark`）配置，缺少白天（Light）模式的专属调色板与壁纸定义。当用户处于白天模式时无法获得和谐的浅色视觉体验；且需无缝兼容现有单模态 v1 主题包，避免造成生态割裂。

如何设计 `.ummtheme v2` 双态数据模型，并实现针对旧版 v1 主题的自动升权与浅色算法派生引擎？

## 验收条件

- [x] 扩展 `CustomTheme` 数据模型，支持显式日间配置属性：`LightBackgroundColor`、`LightCardBackgroundColor`、`LightBackgroundAsset` 等
- [x] 实现针对 v1 单模态主题的智能升级器：若主题缺少日间配置，算法自动基于主强调色推导出温润柔和、高对比度的浅色白天代币（如粉色主题派生为柔白粉底 #FFF5F8、浅粉卡片 #FFEBF2 与暗色清晰文本）
- [x] 扩展 `ThemePackageService`：支持打包与导出包含双壁纸（`wallpaper_dark.png` 与 `wallpaper_light.png`）及双态配置的 `.ummtheme v2` 标准包
- [x] 安全沙箱支持验证并放行双壁纸资源（确保依然拦截恶意脚本与越界路径）
- [x] 编写单元测试覆盖：v2 双模包解析、双壁纸提取、v1 旧包自动算法派生为双模对象

## 答案

1. 在 `CustomTheme` 中扩展了 `ThemeVersion = 2`、`LightBackgroundColor`、`LightCardBackgroundColor`、`LightCardOpacity`、`LightCardBorderRadius`、`LightBackgroundAsset` 与 `HasExplicitLightMode` 属性及参数范围校验；
2. 实现 `EnsureDualMode()` 算法升级器：若主题缺少日间配置，基于 `AccentColor` 与纯白进行 5% 与 12% 混合柔化计算，派生高对比度浅色调底色与卡片色，使旧版 v1 单模态主题包零配置无缝升级为 v2 双模规格；
3. 升级 `ThemePackageService`，支持 `ExportDualModePackage` 导出双壁纸（`wallpaper_dark.*` 与 `wallpaper_light.*`）并兼容单壁纸，`InspectPackagePreview` 与 `ImportPackage` 支持安全解压并提取双壁纸内存流；
4. 编写 `ThemePackageDualModeTests.cs`（7 项专项测试）覆盖双模打包解析、v1 升级算法、双壁纸提取及沙箱恶意脚本拦截，全部绿灯通过。
