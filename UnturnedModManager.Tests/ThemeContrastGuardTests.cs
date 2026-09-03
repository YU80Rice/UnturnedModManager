using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UnturnedModManager.Helpers;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Wpf.Ui.Appearance;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class ThemeContrastGuardTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    _ = new Application();
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

    [Theory]
    [InlineData(ThemePreference.Dark)]
    [InlineData(ThemePreference.Light)]
    public void AllPalettes_ComplyWithWcagAaContrastRequirements(ThemePreference mode)
    {
        foreach (var palette in Enum.GetValues<ThemePalette>())
        {
            var colors = ThemeService.GetPaletteColors(palette, mode)
                .ToDictionary(item => item.Key, item => item.Color, StringComparer.Ordinal);

            var primaryText = (Color)ColorConverter.ConvertFromString(colors["TextFillColorPrimaryBrush"])!;
            var secondaryText = (Color)ColorConverter.ConvertFromString(colors["TextFillColorSecondaryBrush"])!;
            var bg = (Color)ColorConverter.ConvertFromString(colors["ApplicationBackgroundBrush"])!;
            var card = (Color)ColorConverter.ConvertFromString(colors["ControlFillColorDefaultBrush"])!;

            // 1. 正文在背景和卡片上的对比度必须 ≥ 4.5:1 (WCAG AA)
            var textOnBg = ColorContrastHelper.GetContrastRatio(primaryText, bg);
            var textOnCard = ColorContrastHelper.GetContrastRatio(primaryText, card);
            Assert.True(textOnBg >= 4.5, $"{palette}/{mode} text lacks contrast on bg: {textOnBg:F2}:1");
            Assert.True(textOnCard >= 4.5, $"{palette}/{mode} text lacks contrast on card: {textOnCard:F2}:1");

            // 2. 次级文本在卡片上的对比度必须 ≥ 3.0:1 (WCAG Large/Secondary)
            var secOnCard = ColorContrastHelper.GetContrastRatio(secondaryText, card);
            Assert.True(secOnCard >= 3.0, $"{palette}/{mode} secondary text lacks contrast on card: {secOnCard:F2}:1");
        }
    }

    [Fact]
    public void CustomThemes_DualMode_ComplyWithWcagAaContrastRequirements()
    {
        var themes = new[]
        {
            new CustomTheme
            {
                Id = "sakura_pink",
                Name = "樱花粉调",
                AccentColor = "#D83B7E",
                BackgroundColor = "#1A1418",
                CardBackgroundColor = "#261D23",
                LightBackgroundColor = "#FFF2F6",
                LightCardBackgroundColor = "#FFFFFF"
            },
            new CustomTheme
            {
                Id = "cyberpunk_neon",
                Name = "赛博霓虹",
                AccentColor = "#00D2FF",
                BackgroundColor = "#0F111A",
                CardBackgroundColor = "#1A1D2C"
            },
            new CustomTheme
            {
                Id = "emerald_forest",
                Name = "翡翠深林",
                AccentColor = "#10B981",
                BackgroundColor = "#064E3B",
                CardBackgroundColor = "#047857"
            }
        };

        foreach (var theme in themes)
        {
            theme.EnsureDualMode();

            // 1. 夜间模式对比度守卫
            var darkText = Colors.White;
            var darkBg = (Color)ColorConverter.ConvertFromString(theme.BackgroundColor)!;
            var darkCard = (Color)ColorConverter.ConvertFromString(theme.CardBackgroundColor)!;
            Assert.True(ColorContrastHelper.GetContrastRatio(darkText, darkBg) >= 4.5,
                $"Theme '{theme.Name}' Dark Background lacks contrast against White text.");
            Assert.True(ColorContrastHelper.GetContrastRatio(darkText, darkCard) >= 4.5,
                $"Theme '{theme.Name}' Dark Card lacks contrast against White text.");

            // 2. 白天模式对比度守卫
            var lightText = (Color)ColorConverter.ConvertFromString("#1F1F1F")!;
            var lightBg = (Color)ColorConverter.ConvertFromString(theme.LightBackgroundColor!)!;
            var lightCard = (Color)ColorConverter.ConvertFromString(theme.LightCardBackgroundColor!)!;
            Assert.True(ColorContrastHelper.GetContrastRatio(lightText, lightBg) >= 4.5,
                $"Theme '{theme.Name}' Light Background lacks contrast against Dark text.");
            Assert.True(ColorContrastHelper.GetContrastRatio(lightText, lightCard) >= 4.5,
                $"Theme '{theme.Name}' Light Card lacks contrast against Dark text.");
        }
    }

    [Fact]
    public void ThemeApplication_EliminatesDefaultBlueLeaking_InWpfUiAndAppResources()
    {
        RunOnStaThread(() =>
        {
            var app = Application.Current ?? new Application();
            var themeService = new ThemeService();

            var pinkTheme = new CustomTheme
            {
                Id = "sakura_pink_guard",
                Name = "SakuraPinkGuard",
                AccentColor = "#D83B7E",
                BackgroundColor = "#1A1418",
                CardBackgroundColor = "#261D23",
                LightBackgroundColor = "#FFF2F6",
                LightCardBackgroundColor = "#FFFFFF"
            };

            // Act: Apply custom pink theme
            themeService.ApplyCustomTheme(pinkTheme);

            var expectedAccent = (Color)ColorConverter.ConvertFromString("#D83B7E")!;
            var defaultBlue = (Color)ColorConverter.ConvertFromString("#0078D4")!;

            // 验证 Wpf.Ui 内部强调色总线已被强力同步
            Assert.Equal(expectedAccent, ApplicationAccentColorManager.PrimaryAccent);
            Assert.NotEqual(defaultBlue, ApplicationAccentColorManager.PrimaryAccent);

            // 验证 Application.Current.Resources 全局资源已被强力覆盖
            Assert.Equal(expectedAccent, (Color)app.Resources["SystemAccentColorPrimary"]!);
            var accentBrush = (SolidColorBrush)app.Resources["AccentFillColorDefaultBrush"]!;
            Assert.Equal(expectedAccent, accentBrush.Color);
        });
    }

    [Fact]
    public void GenerateAndVerify_OfficialSakuraPinkDualModePackage()
    {
        RunOnStaThread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "umm_sakura_gen_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var darkWpPath = Path.Combine(tempDir, "wallpaper_dark.png");
                var lightWpPath = Path.Combine(tempDir, "wallpaper_light.png");

                File.WriteAllBytes(darkWpPath, RenderTestWallpaper(true));
                File.WriteAllBytes(lightWpPath, RenderTestWallpaper(false));

                var officialTheme = new CustomTheme
                {
                    Id = "sakura_pink",
                    Name = "樱花粉调",
                    Author = "UMM Official",
                    Version = "2.0.0",
                    Description = "精雕细琢的双态樱花粉主题。夜间态呈现静谧深李子与柔粉霓虹微光，日间态呈现明澈珍珠白与春樱漫舞。包含日夜双壁纸与全套 WCAG AA 高对比度设计代币。",
                    ThemeVersion = 2,
                    BaseTheme = ThemePreference.Dark,
                    AccentColor = "#D83B7E",
                    BackgroundColor = "#1A1418",
                    CardBackgroundColor = "#261D23",
                    CardOpacity = 0.92,
                    CardBorderRadius = 10,
                    LightBackgroundColor = "#FFF2F6",
                    LightCardBackgroundColor = "#FFFFFF",
                    LightCardOpacity = 0.95,
                    LightCardBorderRadius = 10
                };

                var pkgService = new ThemePackageService();
                var exportedZipPath = Path.Combine(tempDir, "SakuraPink.ummtheme");

                var exportResult = pkgService.ExportDualModePackage(
                    officialTheme,
                    darkWpPath,
                    lightWpPath,
                    exportedZipPath);

                Assert.True(exportResult.Success, exportResult.Message);
                Assert.True(File.Exists(exportedZipPath));

                // 检验向导预览能力
                var preview = pkgService.InspectPackagePreview(exportedZipPath);
                Assert.NotNull(preview);
                Assert.Equal("樱花粉调", preview.Theme.Name);
                Assert.Equal(2, preview.Theme.ThemeVersion);
                Assert.True(preview.Theme.HasExplicitLightMode);
                Assert.NotNull(preview.WallpaperDarkBytes);
                Assert.NotNull(preview.WallpaperLightBytes);

                // 将该官方双态主题包同步输出至项目根目录与测试包目录
                var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
                var targetRootPackage = Path.Combine(repoRoot, "SakuraPink.ummtheme");
                var targetTestPackage = Path.Combine(repoRoot, "test-packages", "SakuraPink.ummtheme");

                if (Directory.Exists(repoRoot))
                {
                    File.Copy(exportedZipPath, targetRootPackage, true);
                    if (!Directory.Exists(Path.GetDirectoryName(targetTestPackage)!))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetTestPackage)!);
                    }
                    File.Copy(exportedZipPath, targetTestPackage, true);
                }

                // 端到端导入并应用
                var importResult = pkgService.ImportPackage(exportedZipPath);
                Assert.True(importResult.Success, importResult.Message);
                Assert.NotNull(importResult.Theme);
                Assert.NotNull(importResult.WallpaperDestinationPath);
                Assert.NotNull(importResult.WallpaperLightDestinationPath);

                var themeService = new ThemeService();
                themeService.ApplyCustomTheme(
                    importResult.Theme,
                    importResult.WallpaperDestinationPath,
                    importResult.WallpaperLightDestinationPath,
                    targetMode: ThemePreference.Dark);

                Assert.Equal(ThemePreference.Dark, themeService.AppliedTheme);
                Assert.Equal(importResult.WallpaperDestinationPath, themeService.CustomWallpaperPath);

                // 热切换至白天
                themeService.Apply(ThemePreference.Light);
                Assert.Equal(ThemePreference.Light, themeService.AppliedTheme);
                Assert.Equal(importResult.WallpaperLightDestinationPath, themeService.CustomWallpaperPath);

                // 热切换回夜晚
                themeService.Apply(ThemePreference.Dark);
                Assert.Equal(ThemePreference.Dark, themeService.AppliedTheme);
                Assert.Equal(importResult.WallpaperDestinationPath, themeService.CustomWallpaperPath);
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

    private static byte[] RenderTestWallpaper(bool isDark)
    {
        const int width = 1920;
        const int height = 1080;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (isDark)
            {
                var baseBrush = new RadialGradientBrush
                {
                    Center = new Point(0.5, 0.5),
                    GradientOrigin = new Point(0.4, 0.3),
                    RadiusX = 0.8,
                    RadiusY = 0.8
                };
                baseBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x2A, 0x1A, 0x24), 0.0));
                baseBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x19, 0x12, 0x17), 0.6));
                baseBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0x10, 0x0A, 0x0E), 1.0));
                dc.DrawRectangle(baseBrush, null, new Rect(0, 0, width, height));

                var bloom1 = new RadialGradientBrush
                {
                    Center = new Point(0.2, 0.3),
                    GradientOrigin = new Point(0.2, 0.3),
                    RadiusX = 0.4,
                    RadiusY = 0.4
                };
                bloom1.GradientStops.Add(new GradientStop(Color.FromArgb(0x40, 0xD8, 0x3B, 0x7E), 0.0));
                bloom1.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xD8, 0x3B, 0x7E), 1.0));
                dc.DrawEllipse(bloom1, null, new Point(width * 0.2, height * 0.3), width * 0.4, height * 0.4);
            }
            else
            {
                var baseBrush = new LinearGradientBrush(
                    Color.FromRgb(0xFF, 0xF5, 0xF8),
                    Color.FromRgb(0xFF, 0xEB, 0xF2),
                    new Point(0, 0),
                    new Point(1, 1));
                dc.DrawRectangle(baseBrush, null, new Rect(0, 0, width, height));

                var glow = new RadialGradientBrush
                {
                    Center = new Point(0.8, 0.2),
                    GradientOrigin = new Point(0.8, 0.2),
                    RadiusX = 0.6,
                    RadiusY = 0.6
                };
                glow.GradientStops.Add(new GradientStop(Color.FromArgb(0x35, 0xFF, 0xB6, 0xC1), 0.0));
                glow.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xB6, 0xC1), 1.0));
                dc.DrawEllipse(glow, null, new Point(width * 0.8, height * 0.2), width * 0.6, height * 0.6);
            }
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
