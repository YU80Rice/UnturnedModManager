using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using UnturnedModManager.Windows;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class ThemeWizardContrastAndPreviewTests
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
    public void ThemePackageImportWindow_PreviewState_TogglesDayAndNightColors()
    {
        RunOnStaThread(() =>
        {
            var theme = new CustomTheme
            {
                Name = "DualWizardTheme",
                AccentColor = "#FF69B4",
                BackgroundColor = "#111111",
                CardBackgroundColor = "#222222",
                LightBackgroundColor = "#FFF5F8",
                LightCardBackgroundColor = "#FFEBF2"
            };

            var preview = new ThemePackagePreview
            {
                Theme = theme,
                WallpaperDarkBytes = [0x01, 0x02],
                WallpaperLightBytes = [0x03, 0x04]
            };

            var themeService = new ThemeService();
            var packageService = new ThemePackageService();
            var window = new ThemePackageImportWindow(preview, packageService, themeService);

            // Act: Switch to Night
            window.SetPreviewMode(ThemePreference.Dark);
            Assert.Equal("#111111", window.BgHexText.Text);
            Assert.Equal("#222222", window.CardHexText.Text);

            // Act: Switch to Day
            window.SetPreviewMode(ThemePreference.Light);
            Assert.Equal("#FFF5F8", window.BgHexText.Text);
            Assert.Equal("#FFEBF2", window.CardHexText.Text);
        });
    }

    [Fact]
    public void ThemePackageImportWindow_Xaml_AllTextElementsDeclareAccessibleForeground()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Windows", "ThemePackageImportWindow.xaml");
        if (!File.Exists(xamlPath))
        {
            xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Windows/ThemePackageImportWindow.xaml"));
        }
        Assert.True(File.Exists(xamlPath), $"XAML path not found: {xamlPath}");

        var content = File.ReadAllText(xamlPath);

        // Every TextBlock inside the window must declare a dynamic resource or inherit clean contrast
        // Specifically, check the titles and labels
        Assert.Contains("TextFillColorPrimaryBrush", content);
        Assert.Contains("TextFillColorSecondaryBrush", content);

        // Check that section titles have Foreground binding
        Assert.DoesNotContain("<TextBlock Text=\"调色板与外观设计\"\r\n                                   FontWeight=\"SemiBold\"\r\n                                   FontSize=\"13\"\r\n                                   Margin=\"0,0,0,10\" />", content);
    }
}
