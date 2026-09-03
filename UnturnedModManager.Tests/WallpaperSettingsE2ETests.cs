using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media.Imaging;
using UnturnedModManager.Helpers;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class WallpaperSettingsE2ETests
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
    public void SettingsViewModel_WallpaperInteractions_UpdatesSettingsAndNotifies()
    {
        RunOnStaThread(() =>
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "umm_vm_wp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var testImagePath = Path.Combine(tempDir, "sample.png");
            var corruptImagePath = Path.Combine(tempDir, "corrupt.png");

            try
            {
                // Create valid test png
                var rtb = new RenderTargetBitmap(50, 50, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(testImagePath))
                {
                    encoder.Save(fs);
                }

                File.WriteAllBytes(corruptImagePath, [0x00, 0x11, 0x22]);

                var gamePaths = new GamePathService();
                var folderPicker = new DummyFolderPicker();
                var themes = new ThemeService();
                var auth = new CommunityAuthService();
                var dialogs = new DummyDialogService();
                var vm = new SettingsViewModel(gamePaths, folderPicker, themes, auth, dialogs);

                // Initial state
                AppSettings.LauncherCustomWallpaperPath = null;
                AppSettings.LauncherWallpaperBlurRadius = 15.0;
                AppSettings.LauncherWallpaperDimOpacity = 0.35;
                vm.Load();

                Assert.Null(vm.CurrentWallpaperPath);
                Assert.Equal(15.0, vm.WallpaperBlurRadius);
                Assert.Equal(35.0, vm.WallpaperDimPercent);
                Assert.Contains("15 px", vm.WallpaperBlurLabelText);
                Assert.Equal("35%", vm.WallpaperDimLabelText);

                // 1. Set valid wallpaper
                vm.SetCustomWallpaper(testImagePath);
                Assert.Equal(testImagePath, AppSettings.LauncherCustomWallpaperPath);
                Assert.Equal(testImagePath, vm.CurrentWallpaperPath);

                // 2. Try setting corrupt image
                UserNotice? receivedNotice = null;
                vm.NoticeRaised += n => receivedNotice = n;
                vm.SetCustomWallpaper(corruptImagePath);
                Assert.NotNull(receivedNotice);
                Assert.Equal(UserNoticeSeverity.Warning, receivedNotice.Severity);
                Assert.Equal(testImagePath, vm.CurrentWallpaperPath); // unchanged

                // 3. Update blur radius
                vm.UpdateWallpaperBlur(28.0);
                Assert.Equal(28.0, AppSettings.LauncherWallpaperBlurRadius);
                Assert.Equal(28.0, vm.WallpaperBlurRadius);
                Assert.Contains("28 px", vm.WallpaperBlurLabelText);

                // 4. Update dim percent
                vm.UpdateWallpaperDimPercent(65.0);
                Assert.Equal(0.65, AppSettings.LauncherWallpaperDimOpacity);
                Assert.Equal(65.0, vm.WallpaperDimPercent);
                Assert.Equal("65%", vm.WallpaperDimLabelText);

                // 5. Clear wallpaper
                vm.ClearCustomWallpaper();
                Assert.Null(AppSettings.LauncherCustomWallpaperPath);
                Assert.Null(vm.CurrentWallpaperPath);

                vm.Dispose();
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
    public void SettingsPage_Xaml_ContainsWallpaperPersonalizationControls()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        string? targetFile = null;
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "Pages", "SettingsPage.xaml");
            if (File.Exists(candidate)) { targetFile = candidate; break; }
            var subCandidate = Path.Combine(current.FullName, "UnturnedModManager", "Pages", "SettingsPage.xaml");
            if (File.Exists(subCandidate)) { targetFile = subCandidate; break; }
            current = current.Parent;
        }

        Assert.NotNull(targetFile);
        var content = File.ReadAllText(targetFile);

        Assert.Contains("SelectWallpaperButton", content);
        Assert.Contains("ClearWallpaperButton", content);
        Assert.Contains("WallpaperBlurSlider", content);
        Assert.Contains("WallpaperDimSlider", content);
    }

    private sealed class DummyFolderPicker : IFolderPickerService
    {
        public string? PickFolder(string? initialPath, string description) => null;
    }

    private sealed class DummyDialogService : IUserDialogService
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
    }
}
