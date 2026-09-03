using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class ThemeRuntimeSwitchingTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    _ = new System.Windows.Application();
                }
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
        {
            throw new Exception("STA test execution failed", exception);
        }
    }

    [Fact]
    public void Apply_WithActiveCustomTheme_TogglesDayNightModeWithoutDroppingCustomTheme()
    {
        RunOnStaThread(() =>
        {
            var themeService = new ThemeService();
            var theme = new CustomTheme
            {
                Id = "custom_test_dual",
                Name = "DualSwitchTest",
                AccentColor = "#FF69B4",
                BackgroundColor = "#1D161A",
                CardBackgroundColor = "#291F25",
                LightBackgroundColor = "#FFF5F8",
                LightCardBackgroundColor = "#FFEBF2",
                BaseTheme = ThemePreference.Dark
            };

            // Act 1: Apply custom theme in Dark mode
            themeService.ApplyCustomTheme(theme);
            Assert.NotNull(themeService.CurrentCustomTheme);
            Assert.Equal("DualSwitchTest", themeService.CurrentCustomTheme.Name);

            // Act 2: User switches mode to Light
            themeService.Apply(ThemePreference.Light);

            // Assert: Theme must NOT be dropped!
            Assert.NotNull(themeService.CurrentCustomTheme);
            Assert.Equal("DualSwitchTest", themeService.CurrentCustomTheme.Name);
            Assert.Equal(ThemePreference.Light, themeService.AppliedTheme);

            // App resources should now have the light background and card colors
            var app = System.Windows.Application.Current;
            Assert.NotNull(app);
            var bgBrush = app.Resources["ApplicationBackgroundBrush"] as SolidColorBrush;
            Assert.NotNull(bgBrush);
            Assert.Equal(System.Windows.Media.ColorConverter.ConvertFromString("#FFF5F8"), bgBrush.Color);

            // Act 3: User switches back to Dark
            themeService.Apply(ThemePreference.Dark);
            Assert.NotNull(themeService.CurrentCustomTheme);
            Assert.Equal(ThemePreference.Dark, themeService.AppliedTheme);

            var darkBgBrush = app.Resources["ApplicationBackgroundBrush"] as SolidColorBrush;
            Assert.NotNull(darkBgBrush);
            Assert.Equal(System.Windows.Media.ColorConverter.ConvertFromString("#1D161A"), darkBgBrush.Color);
        });
    }

    [Fact]
    public void Apply_WithDualWallpapers_SwapsWallpaperPathBetweenDayAndNight()
    {
        RunOnStaThread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UMM_WallpaperSwap_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var darkWp = Path.Combine(tempDir, "dark.png");
                var lightWp = Path.Combine(tempDir, "light.png");
                File.WriteAllBytes(darkWp, [0x89, 0x50, 0x4E, 0x47]);
                File.WriteAllBytes(lightWp, [0x89, 0x50, 0x4E, 0x47]);

                var themeService = new ThemeService();
                var theme = new CustomTheme
                {
                    Name = "WallpaperTheme",
                    AccentColor = "#0078D4",
                    BackgroundColor = "#111111",
                    CardBackgroundColor = "#222222",
                    LightBackgroundColor = "#EEEEEE",
                    LightCardBackgroundColor = "#FFFFFF"
                };

                themeService.ApplyCustomTheme(theme, darkWp, lightWp);

                // In Dark mode, wallpaper must be dark
                themeService.Apply(ThemePreference.Dark);
                Assert.Equal(darkWp, themeService.CustomWallpaperPath);

                // In Light mode, wallpaper must be light
                themeService.Apply(ThemePreference.Light);
                Assert.Equal(lightWp, themeService.CustomWallpaperPath);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch { }
            }
        });
    }

    [Fact]
    public void ApplyCustomTheme_PersistsToAppSettings_AndResetClearsIt()
    {
        RunOnStaThread(() =>
        {
            var themeService = new ThemeService();
            var theme = new CustomTheme
            {
                Id = "persist_test_theme",
                Name = "PersistTest",
                AccentColor = "#FF69B4",
                BackgroundColor = "#1D161A",
                CardBackgroundColor = "#291F25"
            };

            themeService.ApplyCustomTheme(theme, "/dummy/dark.png", "/dummy/light.png", targetMode: ThemePreference.Dark, persist: true);

            Assert.Equal("persist_test_theme", AppSettings.ActiveCustomThemeId);
            Assert.Equal("/dummy/dark.png", AppSettings.ActiveCustomThemeDarkWallpaper);
            Assert.Equal("/dummy/light.png", AppSettings.ActiveCustomThemeLightWallpaper);

            themeService.ResetToDefaultTheme();

            Assert.Null(AppSettings.ActiveCustomThemeId);
            Assert.Null(AppSettings.ActiveCustomThemeDarkWallpaper);
            Assert.Null(AppSettings.ActiveCustomThemeLightWallpaper);
            Assert.Null(themeService.CurrentCustomTheme);
        });
    }
}
