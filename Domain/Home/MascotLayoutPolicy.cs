using System;

namespace UnturnedModManager.Domain.Home;

/// <summary>
/// 吉祥物安全区与视口布局规约输出
/// </summary>
public sealed record MascotLayoutSpec(
    bool IsVisible,
    double SafeBottomInset,
    double ScaleFactor,
    bool HitTestEnabled
);

/// <summary>
/// 吉祥物动态安全 Insets 与视口防御规约策略
/// </summary>
public static class MascotLayoutPolicy
{
    public const double StandardBottomInset = 96.0;
    public const double MinWindowWidth = 960.0;
    public const double MinWindowHeight = 600.0;
    public const double CriticalWindowWidth = 800.0;
    public const double CriticalWindowHeight = 500.0;

    /// <summary>
    /// 根据小助理显隐状态、当前视口尺寸与 DPI 缩放比，计算安全 Inset 与命中测试规约
    /// </summary>
    public static MascotLayoutSpec Calculate(
        bool isMascotVisible,
        double windowWidth,
        double windowHeight,
        double scaleRatio = 1.0)
    {
        if (scaleRatio <= 0.0) scaleRatio = 1.0;

        if (!isMascotVisible)
        {
            return new MascotLayoutSpec(
                IsVisible: false,
                SafeBottomInset: 0.0,
                ScaleFactor: 0.0,
                HitTestEnabled: false);
        }

        // 视口极端受限：防御性自动隐藏，坚决保证启动卡片与底部交互区 100% 零遮挡
        if (windowWidth < CriticalWindowWidth || windowHeight < CriticalWindowHeight)
        {
            return new MascotLayoutSpec(
                IsVisible: false,
                SafeBottomInset: 0.0,
                ScaleFactor: 0.0,
                HitTestEnabled: false);
        }

        // 低于 960x600 标准基线视口：防御性缩小比例与安全垫片
        if (windowWidth < MinWindowWidth || windowHeight < MinWindowHeight)
        {
            return new MascotLayoutSpec(
                IsVisible: true,
                SafeBottomInset: Math.Round(StandardBottomInset * 0.75 * scaleRatio, 1),
                ScaleFactor: Math.Round(0.75 * scaleRatio, 2),
                HitTestEnabled: true);
        }

        // 标准及高分辨率视口：完整展现
        return new MascotLayoutSpec(
            IsVisible: true,
            SafeBottomInset: Math.Round(StandardBottomInset * scaleRatio, 1),
            ScaleFactor: Math.Round(scaleRatio, 2),
            HitTestEnabled: true);
    }
}
