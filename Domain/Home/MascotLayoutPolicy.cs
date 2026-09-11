using System;

namespace UnturnedModManager.Domain.Home;

/// <summary>
/// 吉祥物测量尺寸与安全余量参数 (实际测量驱动)
/// </summary>
public readonly record struct MascotMeasurements(
    double MeasuredWidth,
    double MeasuredHeight,
    double SafetyPadding = 16.0
)
{
    public static MascotMeasurements Default => new(180.0, 200.0, 16.0);
}

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
/// 吉祥物动态安全 Insets 与视口防御规约策略 (基于挂件实际测量尺寸驱动)
/// </summary>
public static class MascotLayoutPolicy
{
    public const double MinWindowWidth = 960.0;
    public const double MinWindowHeight = 600.0;
    public const double CriticalWindowWidth = 800.0;
    public const double CriticalWindowHeight = 500.0;

    /// <summary>
    /// 根据小助理显隐状态、当前视口尺寸、实际测量尺寸与 DPI 缩放比，计算安全 Inset 与命中测试规约
    /// </summary>
    public static MascotLayoutSpec Calculate(
        bool isMascotVisible,
        double windowWidth,
        double windowHeight,
        MascotMeasurements measurements,
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

        // 视口极端受限 (低于 800x500)：防御性自动隐藏，坚决保证启动卡片与底部交互区 100% 零遮挡
        if (windowWidth < CriticalWindowWidth || windowHeight < CriticalWindowHeight)
        {
            return new MascotLayoutSpec(
                IsVisible: false,
                SafeBottomInset: 0.0,
                ScaleFactor: 0.0,
                HitTestEnabled: false);
        }

        double measuredHeight = measurements.MeasuredHeight > 0 ? measurements.MeasuredHeight : 200.0;
        double safetyPadding = measurements.SafetyPadding >= 0 ? measurements.SafetyPadding : 16.0;

        // 低于 960x600 标准基线视口：防御性缩小比例与安全垫片
        double scaleMultiplier = (windowWidth < MinWindowWidth || windowHeight < MinWindowHeight) ? 0.75 : 1.0;
        double scaleFactor = Math.Round(scaleMultiplier * scaleRatio, 2);

        // 安全底部垫片由实际测量的挂件完整有效高度乘 DPI 缩放比，加上安全留白边距计算驱动，确保 100% 动态避让挂件遮挡
        double effectiveHeight = measuredHeight * scaleMultiplier;
        double safeBottomInset = Math.Round((effectiveHeight + safetyPadding) * scaleRatio, 1);

        return new MascotLayoutSpec(
            IsVisible: true,
            SafeBottomInset: safeBottomInset,
            ScaleFactor: scaleFactor,
            HitTestEnabled: true);
    }

    /// <summary>
    /// 便捷重载：使用默认测量尺寸
    /// </summary>
    public static MascotLayoutSpec Calculate(
        bool isMascotVisible,
        double windowWidth,
        double windowHeight,
        double scaleRatio = 1.0)
        => Calculate(isMascotVisible, windowWidth, windowHeight, MascotMeasurements.Default, scaleRatio);
}
