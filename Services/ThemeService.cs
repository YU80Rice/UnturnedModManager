using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace UnturnedModManager.Services;

public enum ThemePreference { System, Light, Dark }

public sealed class ThemeService
{
    public ThemePreference CurrentPreference { get; private set; } = ThemePreference.System;
    public ThemePreference AppliedTheme { get; private set; } = ThemePreference.Dark;
    public event Action<ThemePreference>? ThemeChanged;
    public void Initialize(string preference) => Apply(Parse(preference), false);
    public void Apply(ThemePreference preference, bool persist = true)
    {
        CurrentPreference = preference;
        var actual = preference == ThemePreference.System ? DetectSystemTheme() : preference;
        AppliedTheme = actual;
        ApplicationThemeManager.Apply(actual == ThemePreference.Light ? ApplicationTheme.Light : ApplicationTheme.Dark, Wpf.Ui.Controls.WindowBackdropType.Mica);
        if (persist) AppSettings.CommunityThemeMode = preference.ToString();
        ThemeChanged?.Invoke(preference);
    }
    public static ThemePreference Parse(string? value) => Enum.TryParse<ThemePreference>(value, true, out var result) ? result : ThemePreference.System;
    private static ThemePreference DetectSystemTheme()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) == 1 ? ThemePreference.Light : ThemePreference.Dark; }
        catch { return ThemePreference.Dark; }
    }
}
