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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

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

        if (CardOpacity is < 0.1 or > 1.0)
            return new(false, "卡片不透明度必须在 0.1 至 1.0 之间。");

        if (CardBorderRadius is < 0.0 or > 32.0)
            return new(false, "卡片圆角大小必须在 0 至 32 之间。");

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
    string? WallpaperDestinationPath = null);
