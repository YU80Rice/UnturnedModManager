using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using UnturnedModManager;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class WallpaperHostTests
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

    [Theory]
    [InlineData("Pages/HomePage.xaml")]
    [InlineData("Pages/SettingsPage.xaml")]
    [InlineData("Pages/ModListPage.xaml")]
    [InlineData("Pages/TaskCenterPage.xaml")]
    [InlineData("Pages/AboutPage.xaml")]
    [InlineData("Pages/CommunityPage.xaml")]
    [InlineData("Pages/CommunityDetailPage.xaml")]
    public void SecondaryPages_RootBackgroundMustBeTransparent(string relativePagePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        string? targetFile = null;
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePagePath);
            if (File.Exists(candidate)) { targetFile = candidate; break; }
            var subCandidate = Path.Combine(current.FullName, "UnturnedModManager", relativePagePath);
            if (File.Exists(subCandidate)) { targetFile = subCandidate; break; }
            current = current.Parent;
        }

        Assert.NotNull(targetFile);
        Assert.True(File.Exists(targetFile), $"Page file not found: {relativePagePath}");
        var content = File.ReadAllText(targetFile);

        // Assert: must use Background="Transparent" to avoid overdrawing
        Assert.Contains("Background=\"Transparent\"", content);
        Assert.DoesNotContain("Background=\"{DynamicResource ApplicationBackgroundBrush}\"", content);
    }

    [Fact]
    public void MainWindow_WallpaperHost_TogglesVisibilityAndAppliesEffects()
    {
        RunOnStaThread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "umm_host_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var testImagePath = Path.Combine(tempDir, "host_wp.png");

            try
            {
                // Render sample image
                var rtb = new RenderTargetBitmap(100, 100, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(testImagePath))
                {
                    encoder.Save(fs);
                }

                var window = new MainWindow();

                // 1. Initial state (no wallpaper)
                AppSettings.LauncherCustomWallpaperPath = null;
                window.RefreshCustomWallpaper();

                Assert.Equal(Visibility.Collapsed, window.WallpaperImage.Visibility);
                Assert.Equal(Visibility.Collapsed, window.WallpaperDimOverlay.Visibility);

                // 2. Set valid wallpaper
                AppSettings.LauncherCustomWallpaperPath = testImagePath;
                AppSettings.LauncherWallpaperBlurRadius = 20.0;
                AppSettings.LauncherWallpaperDimOpacity = 0.45;
                window.RefreshCustomWallpaper();

                Assert.Equal(Visibility.Visible, window.WallpaperImage.Visibility);
                Assert.Equal(Visibility.Visible, window.WallpaperDimOverlay.Visibility);
                Assert.NotNull(window.WallpaperImage.Source);
                Assert.Equal(20.0, window.WallpaperBlurEffect.Radius);
                Assert.Equal(0.45, window.WallpaperDimOverlay.Opacity);

                // 3. Dynamic effect updates
                window.UpdateWallpaperEffects(32.0, 0.65);
                Assert.Equal(32.0, window.WallpaperBlurEffect.Radius);
                Assert.Equal(0.65, window.WallpaperDimOverlay.Opacity);

                // 4. Clear wallpaper
                AppSettings.LauncherCustomWallpaperPath = null;
                window.RefreshCustomWallpaper();

                Assert.Equal(Visibility.Collapsed, window.WallpaperImage.Visibility);
                Assert.Equal(Visibility.Collapsed, window.WallpaperDimOverlay.Visibility);
                Assert.Null(window.WallpaperImage.Source);

                window.Close();
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
}
