using System.Windows;
using System.IO;
using UnturnedModManager.Services;

namespace UnturnedModManager;

public partial class App : System.Windows.Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Services = new AppServices();
            if (!Services.SingleInstance.TryAcquire())
            {
                Shutdown(0);
                return;
            }

            if (!AppSettings.IsOnboardingCompleted)
            {
                var onboarding = new OnboardingWindow();
                onboarding.ShowDialog();
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnturnedModManager");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "startup-error.log"), ex.ToString());
            }
            catch { }
            System.Windows.MessageBox.Show($"启动器初始化失败：\n\n{ex.Message}\n\n诊断日志已保存到 AppData\\Roaming\\UnturnedModManager\\startup-error.log。", "Unturned Mod Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Services.Dispose();
        base.OnExit(e);
    }
}
