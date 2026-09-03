using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Wpf.Ui.Appearance;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class ThemeAccentManagerTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
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
        if (exception is not null)
        {
            throw exception;
        }
    }

    [Fact]
    public void ApplyCustomTheme_SynchronizesWpfUiAccentColorAndApplicationResources()
    {
        RunOnStaThread(() =>
        {
            var app = Application.Current ?? new Application();
            var themeService = new ThemeService();
            var pinkTheme = new CustomTheme
            {
                Name = "SakuraPink",
                AccentColor = "#FF69B4",
                BackgroundColor = "#1D161A",
                CardBackgroundColor = "#291F25",
                BaseTheme = ThemePreference.Dark
            };

            themeService.ApplyCustomTheme(pinkTheme);

            var accentColor = (Color)ColorConverter.ConvertFromString("#FF69B4")!;

            // Verify Wpf.Ui ApplicationAccentColorManager updated
            Assert.Equal(accentColor, ApplicationAccentColorManager.PrimaryAccent);

            // Verify Application Resources contains the updated brush
            Assert.True(app.Resources.Contains("AccentFillColorDefaultBrush"));
            if (app.Resources["AccentFillColorDefaultBrush"] is SolidColorBrush brush)
            {
                Assert.Equal(accentColor, brush.Color);
            }
        });
    }

    [Fact]
    public void ApplyPalette_SynchronizesWpfUiAccentColor()
    {
        RunOnStaThread(() =>
        {
            var app = Application.Current ?? new Application();
            var themeService = new ThemeService();
            
            themeService.ApplyPalette(ThemePalette.MascotOrange);

            var colors = ThemeService.GetPaletteColors(ThemePalette.MascotOrange, themeService.AppliedTheme);
            var expectedHex = colors.First(c => c.Key == "AccentFillColorDefaultBrush").Color;
            var expectedColor = (Color)ColorConverter.ConvertFromString(expectedHex)!;

            // Verify Wpf.Ui ApplicationAccentColorManager updated to MascotOrange
            Assert.Equal(expectedColor, ApplicationAccentColorManager.PrimaryAccent);

            // Verify Application Resources contains the updated brush
            Assert.True(app.Resources.Contains("AccentFillColorDefaultBrush"));
            if (app.Resources["AccentFillColorDefaultBrush"] is SolidColorBrush brush)
            {
                Assert.Equal(expectedColor, brush.Color);
            }
        });
    }
}
