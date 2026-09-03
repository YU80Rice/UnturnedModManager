using System;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace UnturnedModManager.Helpers;

/// <summary>
/// WCAG 2.1 相对亮度与对比度计算辅助类，用于自动化测试守卫与无障碍合规性检测。
/// </summary>
public static class ColorContrastHelper
{
    /// <summary>
    /// 计算 sRGB 颜色的相对亮度 (Relative Luminance)。
    /// 规范：WCAG 2.1 (0.2126*R + 0.7152*G + 0.0722*B，采用非线性 sRGB 转线性伽马校正)。
    /// </summary>
    public static double GetRelativeLuminance(Color color)
    {
        static double ToLinear(byte channel)
        {
            var s = channel / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        var r = ToLinear(color.R);
        var g = ToLinear(color.G);
        var b = ToLinear(color.B);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    /// <summary>
    /// 计算两个颜色之间的对比度 (Contrast Ratio: 1.0 ~ 21.0)。
    /// WCAG AA 标准：常规正文 ≥ 4.5:1，大标题/UI 组件 ≥ 3.0:1。
    /// </summary>
    public static double GetContrastRatio(Color color1, Color color2)
    {
        var l1 = GetRelativeLuminance(color1);
        var l2 = GetRelativeLuminance(color2);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// 解析 Hex 颜色并计算对比度。
    /// </summary>
    public static double GetContrastRatio(string hex1, string hex2)
    {
        var c1 = (Color)ColorConverter.ConvertFromString(hex1)!;
        var c2 = (Color)ColorConverter.ConvertFromString(hex2)!;
        return GetContrastRatio(c1, c2);
    }
}
