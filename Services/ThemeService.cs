using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using UnturnedModManager.Helpers;
using UnturnedModManager.Models;
using Wpf.Ui.Appearance;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace UnturnedModManager.Services;

public enum ThemePreference { System, Light, Dark }
public enum ThemePalette { Fluent, WarmPaper, MascotOrange, MistyForest, OceanDusk, KleinBlue, Lavender }
public sealed record ThemePaletteChoice(ThemePalette Value, string Label);

public sealed class ThemeService
{
    public ThemePreference CurrentPreference { get; private set; } = ThemePreference.System;
    public ThemePreference AppliedTheme { get; private set; } = ThemePreference.Dark;
    public ThemePalette CurrentPalette { get; private set; } = ThemePalette.Fluent;
    public string CurrentAccentHex { get; private set; } = AppSettings.DefaultAccentHex;
    public double CurrentSurfaceTint { get; private set; } = AppSettings.DefaultSurfaceTint;
    public CustomTheme? CurrentCustomTheme { get; private set; }
    public string? CustomWallpaperDarkPath { get; private set; }
    public string? CustomWallpaperLightPath { get; private set; }
    public string? CustomWallpaperPath => AppliedTheme == ThemePreference.Light
        ? CustomWallpaperLightPath ?? CustomWallpaperDarkPath
        : CustomWallpaperDarkPath ?? CustomWallpaperLightPath;

    public event Action<ThemePreference>? ThemeChanged;

    public void Initialize(string preference)
    {
        var mode = Parse(preference);
        CurrentPalette = ParsePalette(AppSettings.CommunityColorPalette);
        CurrentAccentHex = AppSettings.AccentHex;
        CurrentSurfaceTint = AppSettings.SurfaceTint;
        var id = AppSettings.ActiveCustomThemeId;
        var saved = string.IsNullOrWhiteSpace(id) ? null : new ThemePackageService().GetInstalledThemes()
            .FirstOrDefault(theme => theme.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (saved is not null)
        {
            // 用户已保存的新强调色优先；从未迁移过的活动主题由设置层写入初始色。
            CurrentCustomTheme = saved;
            CustomWallpaperDarkPath = AppSettings.ActiveCustomThemeDarkWallpaper;
            CustomWallpaperLightPath = AppSettings.ActiveCustomThemeLightWallpaper;
        }
        else ClearPackage(persist: false);
        Apply(mode, persist: false);
    }

    public void Apply(ThemePreference preference, bool persist = true)
    {
        CurrentPreference = preference;
        SetMode(ResolveMode(preference));
        if (persist) AppSettings.CommunityThemeMode = preference.ToString();
        ApplyResources();
        ThemeChanged?.Invoke(CurrentPreference);
    }

    public static ThemePreference Parse(string? value) =>
        Enum.TryParse<ThemePreference>(value, true, out var result) && Enum.IsDefined(result) ? result : ThemePreference.System;

    public static ThemePalette ParsePalette(string? value) =>
        Enum.TryParse<ThemePalette>(value, true, out var result) && Enum.IsDefined(result) ? result : ThemePalette.Fluent;

    public void ApplyPalette(ThemePalette palette, bool persist = true)
    {
        ClearPackage(persist);
        CurrentPalette = palette;
        if (persist) AppSettings.CommunityColorPalette = palette.ToString();
        ApplyAccent(AppSettings.GetLegacyAccentHex(palette.ToString()), CurrentSurfaceTint, persist);
    }

    public void ApplyAccent(string accentHex, double surfaceTint, bool persist = true)
    {
        if (AppSettings.NormalizeAccentHex(accentHex) is not { } normalized) return;
        ApplyAccentCore(normalized, surfaceTint, persist, raiseChanged: true);
    }

    private void ApplyAccentCore(string normalizedAccentHex, double surfaceTint, bool persist, bool raiseChanged)
    {
        CurrentAccentHex = normalizedAccentHex;
        CurrentSurfaceTint = AppSettings.NormalizeSurfaceTint(surfaceTint);
        if (persist)
        {
            AppSettings.AccentHex = CurrentAccentHex;
            AppSettings.SurfaceTint = CurrentSurfaceTint;
        }
        ApplyResources();
        if (raiseChanged) ThemeChanged?.Invoke(CurrentPreference);
    }

    public void ApplyCustomTheme(CustomTheme theme, string? wallpaperFilePath = null, bool raiseChanged = true) =>
        ApplyCustomTheme(theme, wallpaperFilePath, null, targetMode: null, persist: true, raiseChanged: raiseChanged);

    public void ApplyCustomTheme(CustomTheme theme, string? wallpaperDarkPath, string? wallpaperLightPath, bool raiseChanged = true) =>
        ApplyCustomTheme(theme, wallpaperDarkPath, wallpaperLightPath, targetMode: null, persist: true, raiseChanged: raiseChanged);

    public void ApplyCustomTheme(CustomTheme theme, string? wallpaperDarkPath, string? wallpaperLightPath,
        ThemePreference? targetMode, bool persist = true, bool raiseChanged = true)
    {
        if (!theme.Validate().IsValid) return;
        theme.EnsureDualMode();
        CurrentCustomTheme = theme;
        CustomWallpaperDarkPath = wallpaperDarkPath;
        CustomWallpaperLightPath = wallpaperLightPath;
        // 兼容旧主题包 #AARRGGBB；强调色统一为不透明。
        var accentHex = ToHex(ParseColor(theme.AccentColor.Trim()));
        if (targetMode.HasValue) CurrentPreference = targetMode.Value;
        SetMode(ResolveMode(CurrentPreference));
        if (persist)
        {
            AppSettings.ActiveCustomThemeId = theme.Id;
            AppSettings.ActiveCustomThemeDarkWallpaper = wallpaperDarkPath;
            AppSettings.ActiveCustomThemeLightWallpaper = wallpaperLightPath;
            AppSettings.CommunityThemeMode = CurrentPreference.ToString();
        }
        ApplyAccentCore(accentHex, CurrentSurfaceTint, persist, raiseChanged);
    }

    public void ResetToDefaultTheme()
    {
        ClearPackage(persist: true);
        CurrentPalette = ThemePalette.Fluent;
        AppSettings.CommunityColorPalette = ThemePalette.Fluent.ToString();
        ApplyAccent(AppSettings.DefaultAccentHex, AppSettings.DefaultSurfaceTint);
    }

    private void ClearPackage(bool persist)
    {
        CurrentCustomTheme = null;
        CustomWallpaperDarkPath = null;
        CustomWallpaperLightPath = null;
        if (!persist) return;
        AppSettings.ActiveCustomThemeId = null;
        AppSettings.ActiveCustomThemeDarkWallpaper = null;
        AppSettings.ActiveCustomThemeLightWallpaper = null;
    }

    private ThemePreference ResolveMode(ThemePreference preference) => preference != ThemePreference.System
        ? preference : CurrentCustomTheme?.BaseTheme is ThemePreference.Dark or ThemePreference.Light
            ? CurrentCustomTheme.BaseTheme : DetectSystemTheme();

    private bool _modeApplied;

    private void SetMode(ThemePreference mode)
    {
        if (_modeApplied && AppliedTheme == mode) return;
        AppliedTheme = mode;
        if (System.Windows.Application.Current is null) return;
        ApplicationThemeManager.Apply(mode == ThemePreference.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
            Wpf.Ui.Controls.WindowBackdropType.None);
        _modeApplied = true;
    }

    private bool _wallpaperVisible;

    internal void SetWallpaperPresence(bool visible)
    {
        if (_wallpaperVisible == visible) return;
        _wallpaperVisible = visible;
        ApplyResources();
    }

    private void ApplyResources()
    {
        if (System.Windows.Application.Current is not { } application) return;
        var isDark = AppliedTheme == ThemePreference.Dark;
        var colors = DeriveColors(ParseColor(CurrentAccentHex), isDark, CurrentSurfaceTint, _wallpaperVisible);
        var resources = application.Resources;
        ApplyAccentControlResources(resources, colors, isDark);
        foreach (var (key, color) in colors) SetBrush(resources, key, color);
        resources["ControlCornerRadius"] = new CornerRadius(6);
    }

    internal static IReadOnlyList<(string Key, string Color)> GetPaletteColors(ThemePalette palette, ThemePreference theme) =>
        DeriveColors(ParseColor(AppSettings.GetLegacyAccentHex(palette.ToString())), theme != ThemePreference.Light,
            AppSettings.DefaultSurfaceTint).Select(pair => (pair.Key, FormatColor(pair.Value))).ToArray();

    internal static IReadOnlyDictionary<string, Color> DeriveColors(Color accent, bool dark, double tint, bool wallpaper = false)
    {
        accent = Color.FromRgb(accent.R, accent.G, accent.B);
        var (h, s, _) = ToHsv(accent);
        var t = AppSettings.NormalizeSurfaceTint(tint) / 100 * (s / 100);
        Color Tint(string basis, double sat, double light, double factor) => Blend(ParseColor(basis), FromHsl(h, sat, light), t * factor);
        var bg = dark ? Tint("#1B1C20", 54, 15, .72) : Tint("#F2F4F7", 58, 90, .95);
        var card = dark ? Tint("#24262C", 48, 21, .65) : Tint("#FFFFFF", 48, 98, .45);
        var card2 = dark ? Tint("#2D3038", 46, 26, .68) : Tint("#EFF1F5", 52, 91, .85);
        var card3 = dark ? Tint("#373B44", 44, 32, .70) : Tint("#E3E6EC", 48, 84, .85);
        var border = dark ? Tint("#444854", 40, 42, .75) : Tint("#CFD5DF", 40, 74, .80);
        var text = dark ? Tint("#F5F6FA", 36, 94, .25) : Tint("#1A1D24", 45, 16, .28);
        var secondary = dark ? Tint("#C6CAD4", 28, 76, .30) : Tint("#565B68", 30, 38, .30);
        var tertiary = dark ? Tint("#969BA8", 28, 76, .28) : Tint("#646B7A", 30, 38, .25);

        var luminance = ColorContrastHelper.GetRelativeLuminance(accent);
        var hover = luminance > .35 ? Blend(accent, Colors.Black, .12) : Blend(accent, Colors.White, .14);
        var pressed = luminance > .35 ? Blend(accent, Colors.Black, .20) : Blend(accent, Colors.White, .22);
        var onAccent = GetContrastingForeground(accent);
        var accentText = DeriveAccentText(h, s, dark, card);
        var softBase = accent == Colors.Black || accent == Colors.White || (dark ? luminance < .04 : luminance > .86)
            ? accentText : accent;
        var buttonBorder = ColorContrastHelper.GetContrastRatio(accent, card) < 1.38
            ? dark ? Alpha(Colors.White, .34) : Alpha(ParseColor("#0F172A"), .30)
            : accent;
        var shell = Alpha(bg, wallpaper ? dark ? .72 : .78 : dark ? .94 : .96);
        var danger = ParseColor(dark ? "#FFBCB5" : "#B42318");
        return new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["ApplicationBackgroundBrush"] = bg,
            ["ControlFillColorDefaultBrush"] = Alpha(card, wallpaper ? dark ? .80 : .84 : dark ? .94 : .96),
            ["ControlFillColorSecondaryBrush"] = Alpha(card2, wallpaper ? dark ? .78 : .82 : dark ? .92 : .94),
            ["AppCardBackgroundBrush2"] = card2,
            ["AppCardBackgroundBrush3"] = card3,
            ["ControlStrokeColorDefaultBrush"] = border,
            ["TextFillColorPrimaryBrush"] = text,
            ["TextFillColorSecondaryBrush"] = secondary,
            ["TextFillColorTertiaryBrush"] = tertiary,
            ["AccentFillColorDefaultBrush"] = accent,
            ["AccentFillColorSecondaryBrush"] = hover,
            ["AccentFillColorTertiaryBrush"] = pressed,
            ["TextOnAccentFillColorPrimaryBrush"] = onAccent,
            ["AccentTextFillColorPrimaryBrush"] = accentText,
            ["AppAccentSoftBrush"] = Alpha(softBase, dark ? .20 : .12),
            ["AppAccentBorderBrush"] = Alpha(softBase, dark ? .45 : .35),
            ["AppAccentButtonBorderBrush"] = buttonBorder,
            ["AppSemanticAccentBrush"] = accent,
            ["AppSemanticHoverBrush"] = hover,
            ["AppSemanticTextHighlightBrush"] = accentText,
            ["AppSemanticWindowBackgroundBrush"] = bg,
            ["AppSemanticCardSurfaceBrush"] = card,
            ["AppPillBackgroundBrush"] = dark ? card : Colors.White,
            ["AppPillBorderBrush"] = border,
            ["AppPillSelectedBrush"] = accent,
            ["AppPillSelectedForegroundBrush"] = onAccent,
            ["AppCommandBarSurfaceBrush"] = shell,
            ["AppCommandBarBorderBrush"] = border,
            ["AppMascotGlowBrush"] = Alpha(softBase, dark ? .24 : .16),
            ["AppSuccessTextBrush"] = ParseColor(dark ? "#6EE7A8" : "#0C6B37"),
            ["AppWarningTextBrush"] = ParseColor(dark ? "#FCD34D" : "#9A5B00"),
            ["AppSuccessSurfaceBrush"] = dark ? Alpha(ParseColor("#20B468"), .16) : Alpha(ParseColor("#0C6B37"), .11),
            ["AppWarningSurfaceBrush"] = dark ? Alpha(ParseColor("#F0A020"), .16) : Alpha(ParseColor("#B46E00"), .12),
            ["DangerTextBrush"] = danger,
            ["DangerSurfaceBrush"] = ParseColor(dark ? "#4F1F1B" : "#FFEAE8"),
            ["SystemFillColorCriticalBrush"] = danger,
            ["ToastNotificationBorderBrush"] = accent
        };
    }

    private static Color DeriveAccentText(double h, double s, bool dark, Color card)
    {
        if (s < 8) return ParseColor(dark ? "#D8DCE6" : "#242934");
        var sat = Math.Clamp(s, 36, 90);
        for (int step = 0; step <= (dark ? 11 : 12); step++)
        {
            var candidate = FromHsl(h, sat, dark ? 70 + step * 2 : 42 - step * 2);
            if (ColorContrastHelper.GetContrastRatio(candidate, card) >= 4.8) return candidate;
        }
        return ParseColor(dark ? "#EEF2FA" : "#171C26");
    }

    internal static Color GetContrastingForeground(Color background)
    {
        var ink = ParseColor("#111111");
        return ColorContrastHelper.GetContrastRatio(background, Colors.White) >= ColorContrastHelper.GetContrastRatio(background, ink)
            ? Colors.White : ink;
    }

    internal static (double Hue, double Saturation, double Value) ToHsv(Color color)
    {
        var r = color.R / 255d; var g = color.G / 255d; var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var delta = max - Math.Min(r, Math.Min(g, b));
        var h = delta == 0 ? 0 : max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4;
        return ((h * 60 + 360) % 360, max == 0 ? 0 : delta / max * 100, max * 100);
    }

    internal static Color FromHsv(double hue, double saturation, double value)
    {
        var v = value / 100;
        return FromChroma(hue, v * saturation / 100, v - v * saturation / 100);
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        var l = lightness / 100;
        var c = (1 - Math.Abs(2 * l - 1)) * saturation / 100;
        return FromChroma(hue, c, l - c / 2);
    }

    private static Color FromChroma(double hue, double c, double m)
    {
        var h = (hue % 360 + 360) % 360 / 60;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch
        {
            < 1 => (c, x, 0d), < 2 => (x, c, 0d), < 3 => (0d, c, x),
            < 4 => (0d, x, c), < 5 => (x, 0d, c), _ => (c, 0d, x)
        };
        return Color.FromRgb(Channel((r + m) * 255), Channel((g + m) * 255), Channel((b + m) * 255));
    }

    internal static Color ParseColor(string value) => (Color)ColorConverter.ConvertFromString(value)!;
    internal static string ToHex(Color value) => $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    private static string FormatColor(Color value) => value.A == 255 ? ToHex(value) : value.ToString();
    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
    private static Color Blend(Color source, Color target, double amount) => Color.FromRgb(
        Channel(source.R + (target.R - source.R) * amount),
        Channel(source.G + (target.G - source.G) * amount),
        Channel(source.B + (target.B - source.B) * amount));
    private static Color Alpha(Color color, double alpha) => WithAlpha(color, Channel(255 * alpha));
    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
    private static void SetColor(ResourceDictionary dictionary, string key, Color color) => dictionary[key] = color;
    private static void SetBrush(ResourceDictionary dictionary, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dictionary[key] = brush;
    }

    private static void ApplyAccentControlResources(
        ResourceDictionary dictionary,
        IReadOnlyDictionary<string, Color> colors,
        bool isDark)
    {
        var accent = colors["AccentFillColorDefaultBrush"];
        var pointerOver = colors["AccentFillColorSecondaryBrush"];
        var pressed = colors["AccentFillColorTertiaryBrush"];
        var selection = WithAlpha(accent, isDark ? (byte)82 : (byte)48);
        var primaryText = colors["TextFillColorPrimaryBrush"];
        var onAccent = GetContrastingForeground(accent);
        var onPointerOver = GetContrastingForeground(pointerOver);
        var onPressed = GetContrastingForeground(pressed);

        // WPF-UI can use its own resource dictionary, so sync it as well as the application dictionary.
        // Pass the existing derived shades, not the overload that derives a second palette.
        ApplicationAccentColorManager.Apply(accent, accent, pointerOver, pressed);
        SetColor(dictionary, "SystemAccentColor", accent);
        SetColor(dictionary, "SystemAccentColorPrimary", accent);
        SetColor(dictionary, "SystemAccentColorSecondary", pointerOver);
        SetColor(dictionary, "SystemAccentColorTertiary", pressed);
        SetBrush(dictionary, "SystemAccentColorPrimaryBrush", accent);
        SetBrush(dictionary, "SystemAccentColorSecondaryBrush", pointerOver);
        SetBrush(dictionary, "SystemAccentColorTertiaryBrush", pressed);
        SetBrush(dictionary, "AccentFillColorDefaultBrush", accent);
        SetBrush(dictionary, "AccentFillColorSecondaryBrush", pointerOver);
        SetBrush(dictionary, "AccentFillColorTertiaryBrush", pressed);

        SetBrush(dictionary, "AccentButtonBackground", accent);
        SetBrush(dictionary, "AccentButtonBackgroundPointerOver", pointerOver);
        SetBrush(dictionary, "AccentButtonBackgroundPressed", pressed);
        SetBrush(dictionary, "AccentControlElevationBorderBrush", colors["AppAccentButtonBorderBrush"]);
        SetBrush(dictionary, "AccentButtonBorderBrushPressed", pressed);
        SetBrush(dictionary, "AccentButtonForeground", onAccent);
        SetBrush(dictionary, "AccentButtonForegroundPointerOver", onPointerOver);
        SetBrush(dictionary, "AccentButtonForegroundPressed", onPressed);
        SetBrush(dictionary, "TextOnAccentFillColorPrimaryBrush", onAccent);
        SetBrush(dictionary, "TextOnAccentFillColorSecondaryBrush", onAccent);
        SetBrush(dictionary, "TextOnAccentFillColorSelectedTextBrush", onAccent);

        SetBrush(dictionary, "ToggleSwitchStrokeOn", accent);
        SetBrush(dictionary, "ToggleSwitchStrokeOnPointerOver", pointerOver);
        SetBrush(dictionary, "ToggleSwitchFillOn", accent);
        SetBrush(dictionary, "ToggleSwitchFillOnPointerOver", pointerOver);
        SetBrush(dictionary, "ToggleSwitchKnobFillOn", onAccent);
        SetBrush(dictionary, "ToggleSwitchKnobFillOnPointerOver", onPointerOver);
        SetBrush(dictionary, "ToggleSwitchKnobFillOnPressed", onPressed);

        SetBrush(dictionary, "ToggleButtonBackgroundChecked", accent);
        SetBrush(dictionary, "ToggleButtonBackgroundCheckedPointerOver", pointerOver);
        SetBrush(dictionary, "ToggleButtonBackgroundCheckedPressed", pressed);
        SetBrush(dictionary, "ToggleButtonForegroundChecked", onAccent);
        SetBrush(dictionary, "ToggleButtonForegroundCheckedPointerOver", onPointerOver);
        SetBrush(dictionary, "ToggleButtonForegroundCheckedPressed", onPressed);

        SetBrush(dictionary, "CheckBoxCheckBackgroundFillChecked", accent);
        SetBrush(dictionary, "CheckBoxCheckBackgroundFillCheckedPointerOver", pointerOver);
        SetBrush(dictionary, "CheckBoxCheckBorderBrush", accent);
        SetBrush(dictionary, "CheckBoxCheckGlyphForeground", onAccent);
        SetBrush(dictionary, "RadioButtonOuterEllipseCheckedStroke", accent);
        SetBrush(dictionary, "RadioButtonOuterEllipseCheckedStrokePointerOver", pointerOver);
        SetBrush(dictionary, "RadioButtonCheckGlyphFill", accent);
        SetBrush(dictionary, "RadioButtonOuterEllipseFill", accent);
        SetBrush(dictionary, "RadioButtonOuterEllipseFillPointerOver", pointerOver);

        SetBrush(dictionary, "NavigationViewSelectionIndicatorForeground", accent);
        SetBrush(dictionary, "NavigationViewItemBackgroundSelected", selection);
        SetBrush(dictionary, "NavigationViewItemBackgroundSelectedLeftFluent", selection);
        SetBrush(dictionary, "NavigationViewItemForegroundSelected", primaryText);
        SetBrush(dictionary, "NavigationViewItemForegroundLeftFluent", primaryText);
        SetBrush(dictionary, "ProgressBarForeground", accent);
        SetBrush(dictionary, "ProgressRingForegroundThemeBrush", accent);
        SetBrush(dictionary, "TextControlFocusedBorderBrush", accent);
        SetBrush(dictionary, "ComboBoxBorderBrushFocused", accent);
        SetBrush(dictionary, "FocusStrokeColorOuterBrush", pointerOver);
        SetBrush(dictionary, "FocusStrokeColorInnerBrush", onAccent);

        var disabledForeground = isDark ? Color.FromRgb(165, 165, 165) : Color.FromRgb(115, 115, 115);
        var disabledBackground = isDark ? Color.FromRgb(48, 48, 48) : Color.FromRgb(240, 240, 240);
        var disabledBorder = isDark ? Color.FromRgb(68, 68, 68) : Color.FromRgb(218, 218, 218);

        SetBrush(dictionary, "AccentButtonBackgroundDisabled", disabledBackground);
        SetBrush(dictionary, "AccentButtonBorderBrushDisabled", disabledBorder);
        SetBrush(dictionary, "AccentButtonForegroundDisabled", disabledForeground);

        SetBrush(dictionary, "ButtonBackgroundDisabled", disabledBackground);
        SetBrush(dictionary, "ButtonBorderBrushDisabled", disabledBorder);
        SetBrush(dictionary, "ButtonForegroundDisabled", disabledForeground);
        SetBrush(dictionary, "TextFillColorDisabledBrush", disabledForeground);
        SetBrush(dictionary, "ControlFillColorDisabledBrush", disabledBackground);
    }


    private static ThemePreference DetectSystemTheme()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) == 1 ? ThemePreference.Light : ThemePreference.Dark; }
        catch { return ThemePreference.Dark; }
    }
}
