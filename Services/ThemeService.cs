using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace UnturnedModManager.Services;

public enum ThemePreference { System, Light, Dark }
public enum ThemePalette { Fluent, WarmPaper, MistyForest, OceanDusk, Lavender, KleinBlue }

public sealed record ThemePaletteChoice(ThemePalette Value, string Label);

public sealed class ThemeService
{
    public ThemePreference CurrentPreference { get; private set; } = ThemePreference.System;
    public ThemePreference AppliedTheme { get; private set; } = ThemePreference.Dark;
    public ThemePalette CurrentPalette { get; private set; } = ThemePalette.Fluent;
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
            if (palette != ThemePalette.Fluent)
                ApplyAccentControlResources(dictionary, colors, AppliedTheme == ThemePreference.Dark);
        }

        if (persist) AppSettings.CommunityColorPalette = palette.ToString();
        if (raiseChanged) ThemeChanged?.Invoke(CurrentPreference);
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
        var pointerOver = Blend(accent, Colors.White, isDark ? 0.16 : 0.10);
        var pressed = Blend(accent, Colors.Black, isDark ? 0.18 : 0.12);
        var selection = WithAlpha(accent, isDark ? (byte)82 : (byte)48);
        var onAccent = RelativeLuminance(accent) > 0.47 ? Color.FromRgb(20, 24, 32) : Colors.White;

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
        SetBrush(dictionary, "AccentButtonForegroundPointerOver", onAccent);
        SetBrush(dictionary, "AccentButtonForegroundPressed", onAccent);
        SetBrush(dictionary, "TextOnAccentFillColorPrimaryBrush", onAccent);
        SetBrush(dictionary, "TextOnAccentFillColorSecondaryBrush", onAccent);
        SetBrush(dictionary, "TextOnAccentFillColorSelectedTextBrush", onAccent);

        SetBrush(dictionary, "ToggleSwitchStrokeOn", accent);
        SetBrush(dictionary, "ToggleSwitchStrokeOnPointerOver", pointerOver);
        SetBrush(dictionary, "ToggleSwitchFillOn", accent);
        SetBrush(dictionary, "ToggleSwitchFillOnPointerOver", pointerOver);
        SetBrush(dictionary, "ToggleSwitchKnobFillOn", onAccent);
        SetBrush(dictionary, "ToggleSwitchKnobFillOnPointerOver", onAccent);
        SetBrush(dictionary, "ToggleSwitchKnobFillOnPressed", onAccent);

        SetBrush(dictionary, "ToggleButtonBackgroundChecked", accent);
        SetBrush(dictionary, "ToggleButtonBackgroundCheckedPointerOver", pointerOver);
        SetBrush(dictionary, "ToggleButtonBackgroundCheckedPressed", pressed);
        SetBrush(dictionary, "ToggleButtonForegroundChecked", onAccent);
        SetBrush(dictionary, "ToggleButtonForegroundCheckedPointerOver", onAccent);
        SetBrush(dictionary, "ToggleButtonForegroundCheckedPressed", onAccent);

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
        SetBrush(dictionary, "NavigationViewItemForegroundSelected", accent);
        SetBrush(dictionary, "NavigationViewItemForegroundLeftFluent", accent);
        SetBrush(dictionary, "ProgressBarForeground", accent);
        SetBrush(dictionary, "ProgressRingForegroundThemeBrush", accent);
        SetBrush(dictionary, "TextControlFocusedBorderBrush", accent);
        SetBrush(dictionary, "ComboBoxBorderBrushFocused", accent);
        SetBrush(dictionary, "FocusStrokeColorOuterBrush", pointerOver);
        SetBrush(dictionary, "FocusStrokeColorInnerBrush", onAccent);
    }

    private static void SetColor(ResourceDictionary dictionary, string key, Color color) => dictionary[key] = color;
    private static void SetBrush(ResourceDictionary dictionary, string key, Color color) => dictionary[key] = CreateBrush(color);

    private static Color Blend(Color source, Color target, double amount) => Color.FromRgb(
        (byte)Math.Round(source.R + (target.R - source.R) * amount),
        (byte)Math.Round(source.G + (target.G - source.G) * amount),
        (byte)Math.Round(source.B + (target.B - source.B) * amount));

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static double RelativeLuminance(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;

    private static IReadOnlyList<(string Key, string Color)> GetPaletteColors(ThemePalette palette, ThemePreference theme) =>
        (palette, theme) switch
        {
            (ThemePalette.WarmPaper, ThemePreference.Light) => WarmLight,
            (ThemePalette.WarmPaper, _) => WarmDark,
            (ThemePalette.MistyForest, ThemePreference.Light) => ForestLight,
            (ThemePalette.MistyForest, _) => ForestDark,
            (ThemePalette.OceanDusk, ThemePreference.Light) => OceanLight,
            (ThemePalette.OceanDusk, _) => OceanDark,
            (ThemePalette.Lavender, ThemePreference.Light) => LavenderLight,
            (ThemePalette.Lavender, _) => LavenderDark,
            (ThemePalette.KleinBlue, ThemePreference.Light) => KleinBlueLight,
            (ThemePalette.KleinBlue, _) => KleinBlueDark,
            _ => []
        };

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
        ("AccentTextFillColorPrimaryBrush", "#287CB9")
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
        ("AccentFillColorDefaultBrush", "#56A8E7"),
        ("AccentFillColorSecondaryBrush", "#7DBDEC"),
        ("AccentFillColorTertiaryBrush", "#A7D3F3"),
        ("AccentTextFillColorPrimaryBrush", "#89C8F0")
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
        ("AccentFillColorDefaultBrush", "#4EBB88"),
        ("AccentFillColorSecondaryBrush", "#78CBA5"),
        ("AccentFillColorTertiaryBrush", "#A8DEC1"),
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
        ("AccentFillColorDefaultBrush", "#51B9E9"),
        ("AccentFillColorSecondaryBrush", "#79C9EE"),
        ("AccentFillColorTertiaryBrush", "#A7DCF3"),
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
        ("AccentFillColorDefaultBrush", "#A78DF1"),
        ("AccentFillColorSecondaryBrush", "#C0ACF6"),
        ("AccentFillColorTertiaryBrush", "#D9CDF9"),
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
        ("AccentFillColorDefaultBrush", "#5877FF"),
        ("AccentFillColorSecondaryBrush", "#7D96FF"),
        ("AccentFillColorTertiaryBrush", "#AFBEFF"),
        ("AccentTextFillColorPrimaryBrush", "#93AAFF")
    ];

    private static ThemePreference DetectSystemTheme()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) == 1 ? ThemePreference.Light : ThemePreference.Dark; }
        catch { return ThemePreference.Dark; }
    }
}
