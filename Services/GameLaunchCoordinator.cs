using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Services;

public readonly record struct LaunchOutcome(bool Started, bool Cancelled, string Message);

/// <summary>协调全局启动入口：安装询问、运行模式和实际启动由同一个入口处理。</summary>
public sealed class GameLaunchCoordinator
{
    private readonly GamePathService _paths;
    private readonly BepInExService _bepInEx;
    private readonly GameLaunchService _launcher;
    private readonly LaunchEnvironmentState _environment;
    private readonly IUserDialogService _dialogs;
    private readonly UserNotificationService _notifications;
    private int _launching;

    internal GameLaunchCoordinator(GamePathService paths, BepInExService bepInEx, GameLaunchService launcher,
        LaunchEnvironmentState environment, IUserDialogService dialogs, UserNotificationService notifications)
    {
        _paths = paths;
        _bepInEx = bepInEx;
        _launcher = launcher;
        _environment = environment;
        _dialogs = dialogs;
        _notifications = notifications;
    }

    public bool IsGameRunning => _launcher.IsRunning();
    internal bool IsLaunching => Volatile.Read(ref _launching) != 0;
    public event Action? StateChanged;

    public async Task<LaunchOutcome> LaunchAsync(bool? forceModsEnabled = null)
    {
        if (Interlocked.Exchange(ref _launching, 1) != 0)
            return new(false, false, "已有启动操作正在进行。");

        try
        {
            StateChanged?.Invoke();
            var gamePath = AppSettings.UnturnedInstallPath;
            if (!_paths.IsValid(gamePath))
                return Report(false, "请先在设置中配置有效的 Unturned 安装路径。", UserNoticeSeverity.Warning);
            if (IsGameRunning)
                return Report(false, "检测到 Unturned 已在运行，请勿重复启动。", UserNoticeSeverity.Warning);

            var modsEnabled = forceModsEnabled ?? _environment.GlobalModsEnabled;
            if (modsEnabled && !_bepInEx.GetStatus(gamePath).IsInstalled)
            {
                if (await _dialogs.ConfirmAsync("模组环境未安装", "是否现在安装 BepInEx，然后继续启动模组模式？"))
                {
                    await _bepInEx.DeployAsync(gamePath);
                    if (!_bepInEx.GetStatus(gamePath).IsInstalled)
                        return Report(false, "BepInEx 安装失败：未找到加载器。", UserNoticeSeverity.Error);
                    var enabled = _bepInEx.SetGlobalModsEnabled(gamePath, true);
                    if (!enabled.Success) return Report(false, enabled.Message, UserNoticeSeverity.Error);
                    _environment.GlobalModsEnabled = true;
                    _notifications.Publish(new UserNotice("BepInEx 安装完成。", UserNoticeSeverity.Success));
                }
                else
                {
                    if (!await _dialogs.ConfirmAsync("以纯净模式启动", "是否改为使用 BattlEye 的纯净模式启动？"))
                        return new(false, true, "已取消启动游戏。");
                    modsEnabled = false;
                    _bepInEx.SetGlobalModsEnabled(gamePath, false);
                    _environment.GlobalModsEnabled = false;
                }
            }
            var result = _launcher.Launch(gamePath, modsEnabled);
            return Report(result.Success, result.Message, result.Success ? UserNoticeSeverity.Information : UserNoticeSeverity.Error);
        }
        catch (OperationCanceledException)
        {
            return new(false, true, "已取消启动游戏。");
        }
        catch (Exception exception)
        {
            return Report(false, $"无法启动游戏：{exception.Message}", UserNoticeSeverity.Error);
        }
        finally
        {
            Interlocked.Exchange(ref _launching, 0);
            try { _environment.Refresh(); }
            finally { StateChanged?.Invoke(); }
        }
    }

    private LaunchOutcome Report(bool started, string message, UserNoticeSeverity severity)
    {
        _notifications.Publish(new UserNotice(message, severity));
        return new(started, false, message);
    }
}
