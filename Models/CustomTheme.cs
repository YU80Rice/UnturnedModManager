using UnturnedModManager.Services;

namespace UnturnedModManager.Models;

public sealed record ThemeValidationResult(bool IsValid, string Message);

public sealed class CustomTheme
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "";
    public ThemePreference BaseTheme { get; set; } = ThemePreference.Dark;
    public string AccentColor { get; set; } = "#0078D4";
    public string BackgroundColor { get; set; } = "#1E1E1E";
    public string CardBackgroundColor { get; set; } = "#2D2D2D";
    public double CardOpacity { get; set; } = 0.95;
    public double CardBorderRadius { get; set; } = 10.0;
    public string? BackgroundAsset { get; set; }
    public int ThemeVersion { get; set; } = 2;

    // --- 日间（白天模式）专有设计代币 ---
    public string? LightBackgroundColor { get; set; }
    public string? LightCardBackgroundColor { get; set; }
    public double? LightCardOpacity { get; set; }
    public double? LightCardBorderRadius { get; set; }
    public string? LightBackgroundAsset { get; set; }

    public bool HasExplicitLightMode =>
        !string.IsNullOrWhiteSpace(LightBackgroundColor) && !string.IsNullOrWhiteSpace(LightCardBackgroundColor);

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// 若主题仅包含夜间暗色配置（旧版 v1 主题），自动基于主强调色派生出温润柔和、高对比度的浅色白天代币。
    /// </summary>
    public void EnsureDualMode()
    {
        if (HasExplicitLightMode) return;

        try
        {
            var accent = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(AccentColor)!;
            // 背景使用 95% 白 + 5% 主色调微光
            var bgR = (byte)Math.Clamp((int)Math.Round(255 * 0.95 + accent.R * 0.05), 0, 255);
            var bgG = (byte)Math.Clamp((int)Math.Round(255 * 0.95 + accent.G * 0.05), 0, 255);
            var bgB = (byte)Math.Clamp((int)Math.Round(255 * 0.95 + accent.B * 0.05), 0, 255);
            LightBackgroundColor = $"#{bgR:X2}{bgG:X2}{bgB:X2}";

            // 卡片使用 88% 白 + 12% 主色调柔光
            var cardR = (byte)Math.Clamp((int)Math.Round(255 * 0.88 + accent.R * 0.12), 0, 255);
            var cardG = (byte)Math.Clamp((int)Math.Round(255 * 0.88 + accent.G * 0.12), 0, 255);
            var cardB = (byte)Math.Clamp((int)Math.Round(255 * 0.88 + accent.B * 0.12), 0, 255);
            LightCardBackgroundColor = $"#{cardR:X2}{cardG:X2}{cardB:X2}";

            LightCardOpacity ??= CardOpacity;
            LightCardBorderRadius ??= CardBorderRadius;
            LightBackgroundAsset ??= BackgroundAsset;
            ThemeVersion = 2;
        }
        catch
        {
            LightBackgroundColor = "#F8F8F8";
            LightCardBackgroundColor = "#FFFFFF";
            LightCardOpacity = CardOpacity;
            LightCardBorderRadius = CardBorderRadius;
        }
    }

    public ThemeValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return new(false, "主题名称不能为空。");

        if (Name.Length > 48)
            return new(false, "主题名称不能超过 48 个字符。");

        if (!IsValidHexColor(AccentColor))
            return new(false, "强调色必须是有效的 Hex 颜色值（如 #0078D4）。");

        if (!IsValidHexColor(BackgroundColor))
            return new(false, "背景色必须是有效的 Hex 颜色值。");

        if (!IsValidHexColor(CardBackgroundColor))
            return new(false, "卡片背景色必须是有效的 Hex 颜色值。");

        if (!string.IsNullOrWhiteSpace(LightBackgroundColor) && !IsValidHexColor(LightBackgroundColor))
            return new(false, "日间背景色必须是有效的 Hex 颜色值。");

        if (!string.IsNullOrWhiteSpace(LightCardBackgroundColor) && !IsValidHexColor(LightCardBackgroundColor))
            return new(false, "日间卡片背景色必须是有效的 Hex 颜色值。");

        if (CardOpacity is < 0.1 or > 1.0)
            return new(false, "卡片不透明度必须在 0.1 至 1.0 之间。");

        if (LightCardOpacity is not null && LightCardOpacity is < 0.1 or > 1.0)
            return new(false, "日间卡片不透明度必须在 0.1 至 1.0 之间。");

        if (CardBorderRadius is < 0.0 or > 32.0)
            return new(false, "卡片圆角大小必须在 0 至 32 之间。");

        if (LightCardBorderRadius is not null && LightCardBorderRadius is < 0.0 or > 32.0)
            return new(false, "日间卡片圆角大小必须在 0 至 32 之间。");

        return new(true, "验证通过。");
    }

    private static bool IsValidHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var trimmed = hex.Trim();
        if (!trimmed.StartsWith('#') || (trimmed.Length != 7 && trimmed.Length != 9)) return false;
        return trimmed[1..].All(c => "0123456789ABCDEFabcdef".Contains(c));
    }
}

public sealed record ThemePackageOperationResult(
    bool Success,
    string Message,
    CustomTheme? Theme = null,
    string? WallpaperDestinationPath = null,
    string? WallpaperLightDestinationPath = null);

public sealed class ThemePackagePreview
{
    public CustomTheme Theme { get; set; } = new();
    public byte[]? WallpaperBytes
    {
        get => WallpaperDarkBytes ?? WallpaperLightBytes;
        set => WallpaperDarkBytes = value;
    }
    public string? WallpaperFileName
    {
        get => WallpaperDarkFileName ?? WallpaperLightFileName;
        set => WallpaperDarkFileName = value;
    }
    public byte[]? WallpaperDarkBytes { get; set; }
    public string? WallpaperDarkFileName { get; set; }
    public byte[]? WallpaperLightBytes { get; set; }
    public string? WallpaperLightFileName { get; set; }
    public long PackageSizeBytes { get; set; }
    public string PackagePath { get; set; } = string.Empty;
}
