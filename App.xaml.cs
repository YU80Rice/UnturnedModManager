using System.Windows;
using System.IO;
using UnturnedModManager.Services;

namespace UnturnedModManager;

public partial class App : System.Windows.Application
{
    public static AppServices Services { get; private set; } = null!;
    private OnboardingWindow? _onboardingWindow;
    private int? _pendingInstallModId;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Services = new AppServices();
            if (!Services.SingleInstance.TryAcquire(e.Args))
            {
                Shutdown(0);
                return;
            }

            Services.SingleInstance.Activated += OnSecondaryActivation;
            Services.SingleInstance.StartListening();
            ProtocolRegistrar.EnsureRegistered();
            _pendingInstallModId = ProtocolRegistrar.FindInstallIntent(e.Args);

            if (!AppSettings.IsOnboardingCompleted)
                ShowOnboarding(owner: null);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            ConsumePendingInstallIntent(mainWindow);
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
        if (Services is not null)
        {
            Services.SingleInstance.Activated -= OnSecondaryActivation;
            Services.Dispose();
        }
        base.OnExit(e);
    }

    /// <summary>
    /// Reopens the compact setup wizard from Settings without restarting the app or losing the
    /// current page. This is intentionally optional: it never forces an account login.
    /// </summary>
    public void RestartOnboarding()
    {
        if (_onboardingWindow is not null)
        {
            ActivateWindow(_onboardingWindow);
            return;
        }

        AppSettings.IsOnboardingCompleted = false;
        ShowOnboarding(MainWindow);
        if (MainWindow is MainWindow mainWindow)
            mainWindow.ApplyThemeFromSettings();
    }

    private void ShowOnboarding(Window? owner)
    {
        var onboarding = new OnboardingWindow();
        if (owner is not null)
            onboarding.Owner = owner;

        _onboardingWindow = onboarding;
        try { onboarding.ShowDialog(); }
        finally { _onboardingWindow = null; }
    }

    private void OnSecondaryActivation(string[] arguments)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            var installModId = ProtocolRegistrar.FindInstallIntent(arguments);
            if (_onboardingWindow is not null)
            {
                if (installModId is not null)
                    _pendingInstallModId = installModId;
                ActivateWindow(_onboardingWindow);
                return;
            }

            if (MainWindow is MainWindow mainWindow)
            {
                ActivateWindow(mainWindow);
                if (installModId is { } id)
                    mainWindow.OpenCommunityDetail(id);
                return;
            }

            if (installModId is not null)
                _pendingInstallModId = installModId;
        }));
    }

    private void ConsumePendingInstallIntent(MainWindow mainWindow)
    {
        if (_pendingInstallModId is not { } modId)
            return;

        _pendingInstallModId = null;
        mainWindow.OpenCommunityDetail(modId);
    }

    private static void ActivateWindow(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
