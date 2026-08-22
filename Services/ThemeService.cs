using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
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
    public CustomTheme? CurrentCustomTheme { get; private set; }
    public string? CustomWallpaperPath { get; private set; }
    public event Action<ThemePreference>? ThemeChanged;
    public void Initialize(string preference) => Apply(Parse(preference), false);
    public void Apply(ThemePreference preference, bool persist = true)
    {
        CurrentPreference = preference;
        var actual = preference == ThemePreference.System ? DetectSystemTheme() : preference;
        AppliedTheme = actual;
        ApplicationThemeManager.Apply(actual == ThemePreference.Light ? ApplicationTheme.Light : ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica);
        if (persist) AppSettings.CommunityThemeMode = preference.ToString();
        ApplyPalette(ParsePalette(AppSettings.CommunityColorPalette), persist: false, raiseChanged: false);
        ThemeChanged?.Invoke(preference);
    }
    public static ThemePreference Parse(string? value) => Enum.TryParse<ThemePreference>(value, true, out var result) ? result : ThemePreference.System;

    public static ThemePalette ParsePalette(string? value) =>
        Enum.TryParse<ThemePalette>(value, true, out var result) ? result : ThemePalette.Fluent;

    public void ApplyPalette(ThemePalette palette, bool persist = true) =>
        ApplyPalette(palette, persist, raiseChanged: true);

    private void ApplyPalette(ThemePalette palette, bool persist, bool raiseChanged)
    {
        CurrentPalette = palette;
        var application = System.Windows.Application.Current;
        if (application is not null)
        {
            var dictionary = EnsurePaletteDictionary(application);
            dictionary.Clear();
            var colors = GetPaletteColors(palette, AppliedTheme);
            foreach (var (key, color) in colors)
                dictionary[key] = CreateBrush(color);
            // 通知是自定义控件，不能依赖 WPF-UI 主题内部的 Accent 资源查找顺序；
            // 在每次切换方案时显式写入自己的动态资源，确保边框始终同步当前配色。
            dictionary["ToastNotificationBorderBrush"] = CreateBrush(ResolveAccentColor(application, colors));
            ApplyAccentControlResources(dictionary, colors, AppliedTheme == ThemePreference.Dark);
            ApplySemanticStatusResources(dictionary, AppliedTheme == ThemePreference.Dark);
        }

        if (persist) AppSettings.CommunityColorPalette = palette.ToString();
        if (raiseChanged) ThemeChanged?.Invoke(CurrentPreference);
    }

    public void ApplyCustomTheme(CustomTheme theme, string? wallpaperFilePath = null, bool raiseChanged = true)
    {
        var validation = theme.Validate();
        if (!validation.IsValid) return;

        CurrentCustomTheme = theme;
        CustomWallpaperPath = wallpaperFilePath;
        var actual = theme.BaseTheme == ThemePreference.System ? DetectSystemTheme() : theme.BaseTheme;
        AppliedTheme = actual;
        ApplicationThemeManager.Apply(actual == ThemePreference.Light ? ApplicationTheme.Light : ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica);

        var application = System.Windows.Application.Current;
        if (application is not null)
        {
            var dictionary = EnsurePaletteDictionary(application);
            dictionary.Clear();

            var isDark = actual == ThemePreference.Dark;
            var accent = (Color)ColorConverter.ConvertFromString(theme.AccentColor)!;
            var bg = (Color)ColorConverter.ConvertFromString(theme.BackgroundColor)!;
            var cardBg = (Color)ColorConverter.ConvertFromString(theme.CardBackgroundColor)!;
            var cardAlpha = (byte)Math.Clamp((int)(theme.CardOpacity * 255), 25, 255);
            var cardBrush = new SolidColorBrush(Color.FromArgb(cardAlpha, cardBg.R, cardBg.G, cardBg.B));

            var colors = new List<(string Key, string Color)>
            {
                ("ApplicationBackgroundBrush", theme.BackgroundColor),
                ("AccentFillColorDefaultBrush", theme.AccentColor),
                ("TextFillColorPrimaryBrush", isDark ? "#FFFFFF" : "#1F1F1F"),
                ("TextFillColorSecondaryBrush", isDark ? "#D0D0D0" : "#505050"),
                ("TextFillColorTertiaryBrush", isDark ? "#909090" : "#808080"),
                ("ControlStrokeColorDefaultBrush", isDark ? "#3A3A3A" : "#D0D0D0")
            };

            foreach (var (key, colorHex) in colors)
            {
                dictionary[key] = CreateBrush(colorHex);
            }

            dictionary["ControlFillColorDefaultBrush"] = cardBrush;
            dictionary["ControlFillColorSecondaryBrush"] = new SolidColorBrush(Color.FromArgb((byte)Math.Max(20, cardAlpha - 25), cardBg.R, cardBg.G, cardBg.B));
            dictionary["ControlCornerRadius"] = new CornerRadius(theme.CardBorderRadius);
            dictionary["ToastNotificationBorderBrush"] = new SolidColorBrush(accent);

            ApplyAccentControlResources(dictionary, colors, isDark);
            ApplySemanticStatusResources(dictionary, isDark);
        }

        if (raiseChanged) ThemeChanged?.Invoke(CurrentPreference);
    }

    public void ResetToDefaultTheme()
    {
        CurrentCustomTheme = null;
        CustomWallpaperPath = null;
        Apply(ThemePreference.System);
        ApplyPalette(ThemePalette.Fluent);
    }

    private static ResourceDictionary EnsurePaletteDictionary(System.Windows.Application application)
    {
        const string key = "UnturnedModManager.ThemePalette";
        if (application.Resources[key] is ResourceDictionary dictionary)
            return dictionary;

        dictionary = new ResourceDictionary();
        application.Resources[key] = dictionary;
        application.Resources.MergedDictionaries.Add(dictionary);
        return dictionary;
    }

    private static SolidColorBrush CreateBrush(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

    private static SolidColorBrush CreateBrush(Color color) => new(color);

    private static Color ResolveAccentColor(
        System.Windows.Application application,
        IReadOnlyList<(string Key, string Color)> colors)
    {
        var configured = colors.FirstOrDefault(item => item.Key == "AccentFillColorDefaultBrush").Color;
        if (!string.IsNullOrWhiteSpace(configured))
            return (Color)ColorConverter.ConvertFromString(configured)!;

        return application.TryFindResource("SystemAccentColorPrimaryBrush") is SolidColorBrush systemAccent
            ? systemAccent.Color
            : Color.FromRgb(0, 120, 212);
    }

    /// <summary>
    /// WPF-UI 的表面资源与其交互控件资源是两组不同的键。仅修改 AccentFillColor
    /// 会留下系统蓝色的 Primary Button 和 ToggleSwitch；这里把每套调色板的强调色
    /// 同步注入按钮、开关、焦点、导航选中项、复选框、单选框和进度控件。
    /// </summary>
    private static void ApplyAccentControlResources(
        ResourceDictionary dictionary,
        IReadOnlyList<(string Key, string Color)> colors,
        bool isDark)
    {
        var accentHex = colors.First(item => item.Key == "AccentFillColorDefaultBrush").Color;
        var accent = (Color)ColorConverter.ConvertFromString(accentHex)!;
        var pointerOver = Blend(accent, isDark ? Colors.Black : Colors.White, isDark ? 0.10 : 0.10);
        var pressed = Blend(accent, Colors.Black, isDark ? 0.18 : 0.12);
        var selection = WithAlpha(accent, isDark ? (byte)82 : (byte)48);
        var primaryText = (Color)ColorConverter.ConvertFromString(colors.First(item => item.Key == "TextFillColorPrimaryBrush").Color)!;
        var onAccent = GetContrastingForeground(accent);
        var onPointerOver = GetContrastingForeground(pointerOver);
        var onPressed = GetContrastingForeground(pressed);

        SetColor(dictionary, "SystemAccentColorPrimary", accent);
        SetColor(dictionary, "SystemAccentColorSecondary", pointerOver);
        SetColor(dictionary, "SystemAccentColorTertiary", pressed);
        SetBrush(dictionary, "SystemAccentColorPrimaryBrush", accent);
        SetBrush(dictionary, "SystemAccentColorSecondaryBrush", pointerOver);
        SetBrush(dictionary, "SystemAccentColorTertiaryBrush", pressed);

        SetBrush(dictionary, "AccentButtonBackground", accent);
        SetBrush(dictionary, "AccentButtonBackgroundPointerOver", pointerOver);
        SetBrush(dictionary, "AccentButtonBackgroundPressed", pressed);
        SetBrush(dictionary, "AccentControlElevationBorderBrush", accent);
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

    private static void ApplySemanticStatusResources(ResourceDictionary dictionary, bool isDark)
    {
        // 错误信息不能复用固定红色：它必须同时适配浅色与深色表面，且保持可读性。
        var danger = isDark ? Color.FromRgb(255, 188, 181) : Color.FromRgb(180, 35, 24);
        SetBrush(dictionary, "DangerTextBrush", danger);
        SetBrush(dictionary, "DangerSurfaceBrush", isDark ? Color.FromRgb(79, 31, 27) : Color.FromRgb(255, 234, 232));
        SetBrush(dictionary, "SystemFillColorCriticalBrush", danger);
    }

    private static void SetColor(ResourceDictionary dictionary, string key, Color color) => dictionary[key] = color;
    private static void SetBrush(ResourceDictionary dictionary, string key, Color color) => dictionary[key] = CreateBrush(color);

    private static Color Blend(Color source, Color target, double amount) => Color.FromRgb(
        (byte)Math.Round(source.R + (target.R - source.R) * amount),
        (byte)Math.Round(source.G + (target.G - source.G) * amount),
        (byte)Math.Round(source.B + (target.B - source.B) * amount));

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    internal static Color GetContrastingForeground(Color background)
    {
        // 强调色表面使用纯黑/白两端的可读文字，覆盖中等亮度的橙色、绿色等强调色。
        var dark = Colors.Black;
        return ContrastRatio(background, Colors.White) >= ContrastRatio(background, dark) ? Colors.White : dark;
    }

    private static double ContrastRatio(Color first, Color second) =>
        (Math.Max(RelativeLuminance(first), RelativeLuminance(second)) + 0.05) /
        (Math.Min(RelativeLuminance(first), RelativeLuminance(second)) + 0.05);

    private static double RelativeLuminance(Color color)
    {
        static double Normalize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Normalize(color.R) + 0.7152 * Normalize(color.G) + 0.0722 * Normalize(color.B);
    }

    internal static IReadOnlyList<(string Key, string Color)> GetPaletteColors(ThemePalette palette, ThemePreference theme) =>
        (palette, theme) switch
        {
            (ThemePalette.Fluent, ThemePreference.Light) => FluentLight,
            (ThemePalette.Fluent, _) => FluentDark,
            (ThemePalette.WarmPaper, ThemePreference.Light) => WarmLight,
            (ThemePalette.WarmPaper, _) => WarmDark,
            (ThemePalette.MascotOrange, ThemePreference.Light) => MascotOrangeLight,
            (ThemePalette.MascotOrange, _) => MascotOrangeDark,
            (ThemePalette.MistyForest, ThemePreference.Light) => ForestLight,
            (ThemePalette.MistyForest, _) => ForestDark,
            (ThemePalette.OceanDusk, ThemePreference.Light) => OceanLight,
            (ThemePalette.OceanDusk, _) => OceanDark,
            (ThemePalette.Lavender, ThemePreference.Light) => LavenderLight,
            (ThemePalette.Lavender, _) => LavenderDark,
            (ThemePalette.KleinBlue, ThemePreference.Light) => KleinBlueLight,
            (ThemePalette.KleinBlue, _) => KleinBlueDark,
            _ => FluentDark
        };

    private static readonly (string Key, string Color)[] FluentLight =
    [
        ("ApplicationBackgroundBrush", "#F3F3F3"),
        ("ControlFillColorDefaultBrush", "#FFFFFF"),
        ("ControlFillColorSecondaryBrush", "#F3F3F3"),
        ("ControlFillColorTertiaryBrush", "#E5E5E5"),
        ("ControlStrokeColorDefaultBrush", "#D1D1D1"),
        ("TextFillColorPrimaryBrush", "#1A1A1A"),
        ("TextFillColorSecondaryBrush", "#5D5D5D"),
        ("TextFillColorTertiaryBrush", "#707070"),
        ("AccentFillColorDefaultBrush", "#0078D4"),
        ("AccentFillColorSecondaryBrush", "#1A86D9"),
        ("AccentFillColorTertiaryBrush", "#6FBAE9"),
        ("AccentTextFillColorPrimaryBrush", "#005A9E")
    ];

    private static readonly (string Key, string Color)[] FluentDark =
    [
        ("ApplicationBackgroundBrush", "#202020"),
        ("ControlFillColorDefaultBrush", "#2B2B2B"),
        ("ControlFillColorSecondaryBrush", "#323232"),
        ("ControlFillColorTertiaryBrush", "#3B3B3B"),
        ("ControlStrokeColorDefaultBrush", "#454545"),
        ("TextFillColorPrimaryBrush", "#F5F5F5"),
        ("TextFillColorSecondaryBrush", "#C6C6C6"),
        ("TextFillColorTertiaryBrush", "#9D9D9D"),
        ("AccentFillColorDefaultBrush", "#006CBF"),
        ("AccentFillColorSecondaryBrush", "#005A9E"),
        ("AccentFillColorTertiaryBrush", "#004578"),
        ("AccentTextFillColorPrimaryBrush", "#8AD9FF")
    ];

    private static readonly (string Key, string Color)[] WarmLight =
    [
        ("ApplicationBackgroundBrush", "#F5F1E8"),
        ("ControlFillColorDefaultBrush", "#FFFCF5"),
        ("ControlFillColorSecondaryBrush", "#F4EDE2"),
        ("ControlFillColorTertiaryBrush", "#ECE3D6"),
        ("ControlStrokeColorDefaultBrush", "#D9CFBF"),
        ("TextFillColorPrimaryBrush", "#2B2925"),
        ("TextFillColorSecondaryBrush", "#625D55"),
        ("TextFillColorTertiaryBrush", "#877F74"),
        ("AccentFillColorDefaultBrush", "#3C91D5"),
        ("AccentFillColorSecondaryBrush", "#67AAE0"),
        ("AccentFillColorTertiaryBrush", "#9BC9EC"),
        ("AccentTextFillColorPrimaryBrush", "#2675AE")
    ];

    private static readonly (string Key, string Color)[] WarmDark =
    [
        ("ApplicationBackgroundBrush", "#181715"),
        ("ControlFillColorDefaultBrush", "#24211E"),
        ("ControlFillColorSecondaryBrush", "#2C2925"),
        ("ControlFillColorTertiaryBrush", "#35312C"),
        ("ControlStrokeColorDefaultBrush", "#423D35"),
        ("TextFillColorPrimaryBrush", "#F5F0E7"),
        ("TextFillColorSecondaryBrush", "#CCC4B8"),
        ("TextFillColorTertiaryBrush", "#A79E92"),
        ("AccentFillColorDefaultBrush", "#2C78B8"),
        ("AccentFillColorSecondaryBrush", "#24699F"),
        ("AccentFillColorTertiaryBrush", "#1E5B8A"),
        ("AccentTextFillColorPrimaryBrush", "#89C8F0")
    ];

    private static readonly (string Key, string Color)[] MascotOrangeLight =
    [
        ("ApplicationBackgroundBrush", "#FFF5EC"),
        ("ControlFillColorDefaultBrush", "#FFFDF9"),
        ("ControlFillColorSecondaryBrush", "#FDEBDC"),
        ("ControlFillColorTertiaryBrush", "#F7DCC7"),
        ("ControlStrokeColorDefaultBrush", "#E8C5A8"),
        ("TextFillColorPrimaryBrush", "#3A2516"),
        ("TextFillColorSecondaryBrush", "#6D4A31"),
        ("TextFillColorTertiaryBrush", "#987158"),
        ("AccentFillColorDefaultBrush", "#C86416"),
        ("AccentFillColorSecondaryBrush", "#DF7E31"),
        ("AccentFillColorTertiaryBrush", "#F2B889"),
        ("AccentTextFillColorPrimaryBrush", "#A34E0E")
    ];

    private static readonly (string Key, string Color)[] MascotOrangeDark =
    [
        ("ApplicationBackgroundBrush", "#21170F"),
        ("ControlFillColorDefaultBrush", "#2D2016"),
        ("ControlFillColorSecondaryBrush", "#39291C"),
        ("ControlFillColorTertiaryBrush", "#463321"),
        ("ControlStrokeColorDefaultBrush", "#5B412B"),
        ("TextFillColorPrimaryBrush", "#FFF1E5"),
        ("TextFillColorSecondaryBrush", "#E7C7AD"),
        ("TextFillColorTertiaryBrush", "#BF9D80"),
        ("AccentFillColorDefaultBrush", "#B9540F"),
        ("AccentFillColorSecondaryBrush", "#A94D0D"),
        ("AccentFillColorTertiaryBrush", "#8D3F0B"),
        ("AccentTextFillColorPrimaryBrush", "#FFB67F")
    ];

    private static readonly (string Key, string Color)[] ForestLight =
    [
        ("ApplicationBackgroundBrush", "#EEF3ED"),
        ("ControlFillColorDefaultBrush", "#FBFEFA"),
        ("ControlFillColorSecondaryBrush", "#E4ECE2"),
        ("ControlFillColorTertiaryBrush", "#D7E1D5"),
        ("ControlStrokeColorDefaultBrush", "#C6D3C3"),
        ("TextFillColorPrimaryBrush", "#25332A"),
        ("TextFillColorSecondaryBrush", "#536258"),
        ("TextFillColorTertiaryBrush", "#7A8980"),
        ("AccentFillColorDefaultBrush", "#2C8A62"),
        ("AccentFillColorSecondaryBrush", "#58A983"),
        ("AccentFillColorTertiaryBrush", "#A7D3BA"),
        ("AccentTextFillColorPrimaryBrush", "#247553")
    ];

    private static readonly (string Key, string Color)[] ForestDark =
    [
        ("ApplicationBackgroundBrush", "#151B17"),
        ("ControlFillColorDefaultBrush", "#202921"),
        ("ControlFillColorSecondaryBrush", "#29342A"),
        ("ControlFillColorTertiaryBrush", "#344035"),
        ("ControlStrokeColorDefaultBrush", "#435047"),
        ("TextFillColorPrimaryBrush", "#EFF5EC"),
        ("TextFillColorSecondaryBrush", "#C4D0C3"),
        ("TextFillColorTertiaryBrush", "#9BAA9D"),
        ("AccentFillColorDefaultBrush", "#257A52"),
        ("AccentFillColorSecondaryBrush", "#287B54"),
        ("AccentFillColorTertiaryBrush", "#1E6745"),
        ("AccentTextFillColorPrimaryBrush", "#92D9B1")
    ];

    private static readonly (string Key, string Color)[] OceanLight =
    [
        ("ApplicationBackgroundBrush", "#EDF4F7"),
        ("ControlFillColorDefaultBrush", "#FBFEFF"),
        ("ControlFillColorSecondaryBrush", "#E1EDF2"),
        ("ControlFillColorTertiaryBrush", "#D3E2E9"),
        ("ControlStrokeColorDefaultBrush", "#BFD2DC"),
        ("TextFillColorPrimaryBrush", "#20323D"),
        ("TextFillColorSecondaryBrush", "#4E6573"),
        ("TextFillColorTertiaryBrush", "#788D99"),
        ("AccentFillColorDefaultBrush", "#267FAE"),
        ("AccentFillColorSecondaryBrush", "#58A7D1"),
        ("AccentFillColorTertiaryBrush", "#A7D2E8"),
        ("AccentTextFillColorPrimaryBrush", "#216F98")
    ];

    private static readonly (string Key, string Color)[] OceanDark =
    [
        ("ApplicationBackgroundBrush", "#121B22"),
        ("ControlFillColorDefaultBrush", "#1B2931"),
        ("ControlFillColorSecondaryBrush", "#24343E"),
        ("ControlFillColorTertiaryBrush", "#2E414C"),
        ("ControlStrokeColorDefaultBrush", "#3B5360"),
        ("TextFillColorPrimaryBrush", "#EFF7FA"),
        ("TextFillColorSecondaryBrush", "#C3D3D9"),
        ("TextFillColorTertiaryBrush", "#98ADB7"),
        ("AccentFillColorDefaultBrush", "#1A6A98"),
        ("AccentFillColorSecondaryBrush", "#246F99"),
        ("AccentFillColorTertiaryBrush", "#1B5C80"),
        ("AccentTextFillColorPrimaryBrush", "#8DD6F3")
    ];

    private static readonly (string Key, string Color)[] LavenderLight =
    [
        ("ApplicationBackgroundBrush", "#F5F1F9"),
        ("ControlFillColorDefaultBrush", "#FCFAFF"),
        ("ControlFillColorSecondaryBrush", "#EDE7F4"),
        ("ControlFillColorTertiaryBrush", "#E1D9EB"),
        ("ControlStrokeColorDefaultBrush", "#CEC3DC"),
        ("TextFillColorPrimaryBrush", "#302A3A"),
        ("TextFillColorSecondaryBrush", "#625A70"),
        ("TextFillColorTertiaryBrush", "#8B8198"),
        ("AccentFillColorDefaultBrush", "#755EC2"),
        ("AccentFillColorSecondaryBrush", "#9D89D9"),
        ("AccentFillColorTertiaryBrush", "#CABCF0"),
        ("AccentTextFillColorPrimaryBrush", "#684FB1")
    ];

    private static readonly (string Key, string Color)[] LavenderDark =
    [
        ("ApplicationBackgroundBrush", "#1C1922"),
        ("ControlFillColorDefaultBrush", "#27222F"),
        ("ControlFillColorSecondaryBrush", "#312A3B"),
        ("ControlFillColorTertiaryBrush", "#3C3348"),
        ("ControlStrokeColorDefaultBrush", "#4D415B"),
        ("TextFillColorPrimaryBrush", "#F5F0FA"),
        ("TextFillColorSecondaryBrush", "#D0C5D9"),
        ("TextFillColorTertiaryBrush", "#AA9EB5"),
        ("AccentFillColorDefaultBrush", "#6A4FB0"),
        ("AccentFillColorSecondaryBrush", "#795EC0"),
        ("AccentFillColorTertiaryBrush", "#5A4298"),
        ("AccentTextFillColorPrimaryBrush", "#C4B0F8")
    ];

    private static readonly (string Key, string Color)[] KleinBlueLight =
    [
        ("ApplicationBackgroundBrush", "#F2F4FC"),
        ("ControlFillColorDefaultBrush", "#FBFCFF"),
        ("ControlFillColorSecondaryBrush", "#E7EBF7"),
        ("ControlFillColorTertiaryBrush", "#D9E0F1"),
        ("ControlStrokeColorDefaultBrush", "#C5CEE5"),
        ("TextFillColorPrimaryBrush", "#101B43"),
        ("TextFillColorSecondaryBrush", "#465576"),
        ("TextFillColorTertiaryBrush", "#7582A0"),
        ("AccentFillColorDefaultBrush", "#002FA7"),
        ("AccentFillColorSecondaryBrush", "#3F5FC5"),
        ("AccentFillColorTertiaryBrush", "#91A4E6"),
        ("AccentTextFillColorPrimaryBrush", "#00248A")
    ];

    private static readonly (string Key, string Color)[] KleinBlueDark =
    [
        ("ApplicationBackgroundBrush", "#0E1222"),
        ("ControlFillColorDefaultBrush", "#151B31"),
        ("ControlFillColorSecondaryBrush", "#1D2641"),
        ("ControlFillColorTertiaryBrush", "#273255"),
        ("ControlStrokeColorDefaultBrush", "#344263"),
        ("TextFillColorPrimaryBrush", "#F1F4FF"),
        ("TextFillColorSecondaryBrush", "#C4CCE7"),
        ("TextFillColorTertiaryBrush", "#9AA8C7"),
        ("AccentFillColorDefaultBrush", "#3857D6"),
        ("AccentFillColorSecondaryBrush", "#304BB8"),
        ("AccentFillColorTertiaryBrush", "#293F9A"),
        ("AccentTextFillColorPrimaryBrush", "#93AAFF")
    ];

    private static ThemePreference DetectSystemTheme()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) == 1 ? ThemePreference.Light : ThemePreference.Dark; }
        catch { return ThemePreference.Dark; }
    }
}
