using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class ThemePackageImportTests
{
    private static string CreateTestUmmTheme(string name, string accentColor, byte[]? wallpaperBytes = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_theme_{Guid.NewGuid():N}.ummtheme");
        using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Create))
        {
            var theme = new CustomTheme
            {
                Name = name,
                Author = "ThemeDesigner",
                Version = "1.0.0",
                Description = "A vibrant custom theme",
                AccentColor = accentColor,
                BackgroundColor = "#121212",
                CardBackgroundColor = "#1E1E1E",
                BaseTheme = ThemePreference.Dark
            };

            var manifestEntry = zip.CreateEntry("theme.json");
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
            {
                writer.Write(JsonSerializer.Serialize(theme));
            }

            if (wallpaperBytes is not null)
            {
                var imgEntry = zip.CreateEntry("wallpaper.png");
                using var stream = imgEntry.Open();
                stream.Write(wallpaperBytes);
            }
        }
        return tempFile;
    }

    [Fact]
    public void InspectPackagePreview_ExtractsThemeMetadataAndWallpaperPreview()
    {
        var dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var themePath = CreateTestUmmTheme("CyberpunkNeon", "#00FFCC", dummyPng);

        try
        {
            var service = new ThemePackageService();
            var preview = service.InspectPackagePreview(themePath);

            Assert.NotNull(preview);
            Assert.Equal("CyberpunkNeon", preview.Theme.Name);
            Assert.Equal("ThemeDesigner", preview.Theme.Author);
            Assert.Equal("#00FFCC", preview.Theme.AccentColor);
            Assert.NotNull(preview.WallpaperBytes);
            Assert.Equal(dummyPng.Length, preview.WallpaperBytes!.Length);
            Assert.Equal("wallpaper.png", preview.WallpaperFileName);
        }
        finally
        {
            if (File.Exists(themePath)) File.Delete(themePath);
        }
    }

    [Fact]
    public void ImportAndApplyCustomTheme_HotAppliesColorsAndWallpaper()
    {
        var root = Path.Combine(Path.GetTempPath(), "umm-test-theme-apply-" + Guid.NewGuid().ToString("N"));
        var dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var themePath = CreateTestUmmTheme("SunsetWarmth", "#FF5722", dummyPng);

        try
        {
            var packageService = new ThemePackageService(Path.Combine(root, "themes"));
            var importResult = packageService.ImportPackage(themePath);

            Assert.True(importResult.Success);
            Assert.NotNull(importResult.Theme);
            Assert.NotNull(importResult.WallpaperDestinationPath);
            Assert.True(File.Exists(importResult.WallpaperDestinationPath));

            var themeService = new ThemeService();
            themeService.ApplyCustomTheme(importResult.Theme!, importResult.WallpaperDestinationPath);

            Assert.Equal("SunsetWarmth", themeService.CurrentCustomTheme?.Name);
            Assert.Equal("#FF5722", themeService.CurrentCustomTheme?.AccentColor);
            Assert.Equal(importResult.WallpaperDestinationPath, themeService.CustomWallpaperPath);
        }
        finally
        {
            if (File.Exists(themePath)) File.Delete(themePath);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
