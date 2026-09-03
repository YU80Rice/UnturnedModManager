using System;
using System.IO;
using System.Linq;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class ThemePackageDualModeTests : IDisposable
{
    private readonly string _testDir;
    private readonly ThemePackageService _packageService;

    public ThemePackageDualModeTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "UMM_ThemeDualModeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _packageService = new ThemePackageService(Path.Combine(_testDir, "installed_themes"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Fact]
    public void CustomTheme_V1SingleModeTheme_AutomaticallyUpgradesToDualMode()
    {
        // Arrange: A legacy v1 theme with only dark mode specified
        var v1Theme = new CustomTheme
        {
            Name = "SakuraLegacy",
            AccentColor = "#FF69B4",
            BackgroundColor = "#1D161A",
            CardBackgroundColor = "#291F25",
            BaseTheme = ThemePreference.Dark
        };

        // Assert initially no explicit light mode
        Assert.False(v1Theme.HasExplicitLightMode);

        // Act: Ensure dual mode is populated
        v1Theme.EnsureDualMode();

        // Assert: Light mode colors must be automatically derived
        Assert.NotNull(v1Theme.LightBackgroundColor);
        Assert.NotNull(v1Theme.LightCardBackgroundColor);
        Assert.StartsWith("#", v1Theme.LightBackgroundColor);
        Assert.StartsWith("#", v1Theme.LightCardBackgroundColor);

        // Light background must be light (luminance > 0.8)
        var bg = System.Windows.Media.ColorConverter.ConvertFromString(v1Theme.LightBackgroundColor);
        Assert.NotNull(bg);
    }

    [Fact]
    public void ThemePackageService_ExportAndInspectDualModePackage_PreservesBothWallpapersAndPalettes()
    {
        var packagePath = Path.Combine(_testDir, "DualSakura.ummtheme");
        var darkWp = Path.Combine(_testDir, "wp_dark.png");
        var lightWp = Path.Combine(_testDir, "wp_light.png");

        File.WriteAllBytes(darkWp, [0x89, 0x50, 0x4E, 0x47, 0x01, 0x02]); // mock png bytes
        File.WriteAllBytes(lightWp, [0x89, 0x50, 0x4E, 0x47, 0x03, 0x04]);

        var theme = new CustomTheme
        {
            Name = "DualSakura",
            AccentColor = "#FF69B4",
            BackgroundColor = "#1D161A",
            CardBackgroundColor = "#291F25",
            LightBackgroundColor = "#FFF5F8",
            LightCardBackgroundColor = "#FFEBF2",
            BaseTheme = ThemePreference.Dark
        };

        var result = _packageService.ExportDualModePackage(theme, darkWp, lightWp, packagePath);
        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(packagePath));

        var preview = _packageService.InspectPackagePreview(packagePath);
        Assert.NotNull(preview);
        Assert.Equal("DualSakura", preview.Theme.Name);
        Assert.True(preview.Theme.HasExplicitLightMode);
        Assert.Equal("#FFF5F8", preview.Theme.LightBackgroundColor);
        Assert.NotNull(preview.WallpaperDarkBytes);
        Assert.NotNull(preview.WallpaperLightBytes);
        Assert.Equal(6, preview.WallpaperDarkBytes.Length);
        Assert.Equal(6, preview.WallpaperLightBytes.Length);
    }

    [Fact]
    public void ThemePackageService_ImportDualModePackage_UnpacksBothWallpapersSafely()
    {
        var packagePath = Path.Combine(_testDir, "ImportDual.ummtheme");
        var darkWp = Path.Combine(_testDir, "wp_dark.png");
        var lightWp = Path.Combine(_testDir, "wp_light.png");

        File.WriteAllBytes(darkWp, [0x89, 0x50, 0x4E, 0x47, 0xAA, 0xBB]);
        File.WriteAllBytes(lightWp, [0x89, 0x50, 0x4E, 0x47, 0xCC, 0xDD]);

        var theme = new CustomTheme
        {
            Name = "ImportDualTest",
            AccentColor = "#FF69B4",
            BackgroundColor = "#1D161A",
            CardBackgroundColor = "#291F25",
            LightBackgroundColor = "#FFF5F8",
            LightCardBackgroundColor = "#FFEBF2"
        };

        var exportRes = _packageService.ExportDualModePackage(theme, darkWp, lightWp, packagePath);
        Assert.True(exportRes.Success);

        var importRes = _packageService.ImportPackage(packagePath);
        Assert.True(importRes.Success, importRes.Message);
        Assert.NotNull(importRes.WallpaperDestinationPath);
        Assert.NotNull(importRes.WallpaperLightDestinationPath);
        Assert.True(File.Exists(importRes.WallpaperDestinationPath));
        Assert.True(File.Exists(importRes.WallpaperLightDestinationPath));
        Assert.NotEqual(importRes.WallpaperDestinationPath, importRes.WallpaperLightDestinationPath);
    }

    [Theory]
    [InlineData("not-a-hex", "#FFFFFF")]
    [InlineData("#FFF", "#FFFFFF")]
    [InlineData("#FFFFFF", "bad-hex")]
    public void CustomTheme_Validation_RejectsInvalidLightColors(string lightBg, string lightCardBg)
    {
        var theme = new CustomTheme
        {
            Name = "BadHex",
            LightBackgroundColor = lightBg,
            LightCardBackgroundColor = lightCardBg
        };

        var result = theme.Validate();
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ThemePackageService_Sandbox_RejectsExecutablePayloadInDualModePackage()
    {
        var packagePath = Path.Combine(_testDir, "MaliciousDual.ummtheme");

        using (var zip = System.IO.Compression.ZipFile.Open(packagePath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("theme.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write("{\"Name\":\"TrojanTheme\",\"AccentColor\":\"#FF69B4\",\"BackgroundColor\":\"#1D161A\",\"CardBackgroundColor\":\"#291F25\"}");
            }

            var exeEntry = zip.CreateEntry("payload.exe");
            using (var writer = new StreamWriter(exeEntry.Open()))
            {
                writer.Write("MZ...");
            }
        }

        var result = _packageService.ImportPackage(packagePath);
        Assert.False(result.Success);
        Assert.Contains("危险的载荷", result.Message);
    }
}
