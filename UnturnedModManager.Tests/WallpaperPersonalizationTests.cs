using System;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;
using UnturnedModManager.Helpers;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class WallpaperPersonalizationTests
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
    public void AppSettings_WallpaperParameters_PersistAndClampCorrectly()
    {
        // 1. Normal values
        AppSettings.LauncherCustomWallpaperPath = "C:\\test\\wallpaper.png";
        AppSettings.LauncherWallpaperBlurRadius = 25.0;
        AppSettings.LauncherWallpaperDimOpacity = 0.50;

        Assert.Equal("C:\\test\\wallpaper.png", AppSettings.LauncherCustomWallpaperPath);
        Assert.Equal(25.0, AppSettings.LauncherWallpaperBlurRadius);
        Assert.Equal(0.50, AppSettings.LauncherWallpaperDimOpacity);

        // 2. Underflow clamp
        AppSettings.LauncherWallpaperBlurRadius = -10.0;
        AppSettings.LauncherWallpaperDimOpacity = 0.02;
        Assert.Equal(0.0, AppSettings.LauncherWallpaperBlurRadius);
        Assert.Equal(0.10, AppSettings.LauncherWallpaperDimOpacity);

        // 3. Overflow clamp
        AppSettings.LauncherWallpaperBlurRadius = 100.0;
        AppSettings.LauncherWallpaperDimOpacity = 0.99;
        Assert.Equal(40.0, AppSettings.LauncherWallpaperBlurRadius);
        Assert.Equal(0.80, AppSettings.LauncherWallpaperDimOpacity);

        // Reset
        AppSettings.LauncherCustomWallpaperPath = null;
        AppSettings.LauncherWallpaperBlurRadius = 15.0;
        AppSettings.LauncherWallpaperDimOpacity = 0.35;
    }

    [Fact]
    public void WallpaperHelper_LoadNonBlocking_ReleasesFileHandleImmediately()
    {
        RunOnStaThread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "umm_wp_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var testImagePath = Path.Combine(tempDir, "test_wp.png");

            try
            {
                // Create a 100x100 1-bit or 32-bit test png
                var rtb = new RenderTargetBitmap(100, 100, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(testImagePath))
                {
                    encoder.Save(fs);
                }

                // Act: Load image via non-blocking helper
                var bitmap = WallpaperHelper.LoadNonBlocking(testImagePath);
                Assert.NotNull(bitmap);

                // Assert: File handle must be completely released, allowing immediate overwrite or deletion
                File.Delete(testImagePath);
                Assert.False(File.Exists(testImagePath));
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:\\non_existent_file_xyz.png")]
    public void WallpaperHelper_CorruptOrMissingPath_ReturnsNullGracefully(string? invalidPath)
    {
        RunOnStaThread(() =>
        {
            var result = WallpaperHelper.LoadNonBlocking(invalidPath);
            Assert.Null(result);
        });
    }

    [Fact]
    public void WallpaperHelper_CorruptBytes_ReturnsNullGracefully()
    {
        RunOnStaThread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "umm_wp_corrupt_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var corruptPath = Path.Combine(tempDir, "corrupt.png");

            try
            {
                File.WriteAllBytes(corruptPath, [0x01, 0x02, 0x03, 0x04]);
                var result = WallpaperHelper.LoadNonBlocking(corruptPath);
                Assert.Null(result);
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
