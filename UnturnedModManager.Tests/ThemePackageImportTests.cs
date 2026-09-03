using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
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

    [Fact]
    public void GenerateSakuraPinkPackageForUser()
    {
        var outputDir = @"d:\Agent-工作目录\DevelopMyUNMultiplayerModAndModloader\启动器\UnturnedModManager\test-packages";
        Directory.CreateDirectory(outputDir);
        var targetFile = Path.Combine(outputDir, "SakuraPink.ummtheme");
        var rootFile = @"d:\Agent-工作目录\DevelopMyUNMultiplayerModAndModloader\启动器\UnturnedModManager\SakuraPink.ummtheme";

        using var bmp = new System.Drawing.Bitmap(1920, 1080);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new System.Drawing.Rectangle(0, 0, 1920, 1080);
            using var grad = new System.Drawing.Drawing2D.LinearGradientBrush(
                rect,
                System.Drawing.Color.FromArgb(255, 45, 24, 36),
                System.Drawing.Color.FromArgb(255, 20, 14, 18),
                45.0f);
            g.FillRectangle(grad, rect);

            var rand = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                int gx = rand.Next(100, 1820);
                int gy = rand.Next(100, 980);
                int r = rand.Next(120, 320);
                using var glow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(rand.Next(15, 35), 255, 105, 180));
                g.FillEllipse(glow, gx - r / 2, gy - r / 2, r, r);
            }

            for (int i = 0; i < 45; i++)
            {
                int px = rand.Next(50, 1870);
                int py = rand.Next(50, 1030);
                int pw = rand.Next(20, 46);
                int ph = (int)(pw * 0.55);
                using var petal = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(rand.Next(90, 190), 255, 182, 193));
                var state = g.Save();
                g.TranslateTransform(px, py);
                g.RotateTransform(rand.Next(-60, 60));
                g.FillEllipse(petal, -pw / 2, -ph / 2, pw, ph);
                g.Restore(state);
            }

            using var font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold);
            using var textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(90, 255, 255, 255));
            g.DrawString("UNTURNED MOD MANAGER • SAKURA PINK THEME", font, textBrush, 60, 1000);
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var wallpaperBytes = ms.ToArray();

        var theme = new CustomTheme
        {
            Id = "sakura-pink-theme",
            Name = "樱花粉调 (Sakura Pink)",
            Author = "Antigravity",
            Version = "1.0.0",
            Description = "专为 Unturned Mod Manager 打造的柔和樱花粉糖果主题，拥有温润粉嫩的强调色与深李子粉卡片设计，支持全界面即时热应用换肤。",
            BaseTheme = ThemePreference.Dark,
            AccentColor = "#FF69B4",
            BackgroundColor = "#1D161A",
            CardBackgroundColor = "#291F25",
            CardOpacity = 0.92,
            CardBorderRadius = 10.0,
            BackgroundAsset = "sakura_wallpaper.png",
            CreatedAt = DateTimeOffset.Now
        };

        if (File.Exists(targetFile)) File.Delete(targetFile);

        using (var zip = ZipFile.Open(targetFile, ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("theme.json");
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
            {
                writer.Write(JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true }));
            }

            var imgEntry = zip.CreateEntry("sakura_wallpaper.png");
            using (var stream = imgEntry.Open())
            {
                stream.Write(wallpaperBytes);
            }
        }

        File.Copy(targetFile, rootFile, true);
        Assert.True(File.Exists(targetFile));
        Assert.True(File.Exists(rootFile));
    }
}
