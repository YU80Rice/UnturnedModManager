namespace UnturnedModManager.Services;

/// <summary>指挥舱和首页共享的运行环境快照；写入镜像属性不会改动游戏文件。</summary>
public sealed class LaunchEnvironmentState
{
    private readonly BepInExService _bepInEx;
    private readonly DxvkService _dxvk;
    private readonly PluginProfileService _profiles;
    private readonly LocalModService _mods;
    private bool _globalModsEnabled;

    internal LaunchEnvironmentState(BepInExService bepInEx, DxvkService dxvk, PluginProfileService profiles, LocalModService mods)
    {
        _bepInEx = bepInEx;
        _dxvk = dxvk;
        _profiles = profiles;
        _mods = mods;
    }

    public bool GlobalModsEnabled
    {
        get => _globalModsEnabled;
        set
        {
            if (_globalModsEnabled == value) return;
            _globalModsEnabled = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ProfileName { get; private set; } = "";
    public int EnabledPluginCount { get; private set; }
    public int TotalPluginCount { get; private set; }
    public bool IsBepInExReady { get; private set; }
    public bool IsDxvkEnabled { get; private set; }
    private string _loaderText = "BepInEx 未安装";

    public string ProfileSummaryText => $"方案：{(string.IsNullOrWhiteSpace(ProfileName) ? "默认方案" : ProfileName)} · "
        + (!GlobalModsEnabled || TotalPluginCount == 0 ? "0 插件 (纯净)" : $"{EnabledPluginCount}/{TotalPluginCount} 插件");
    public string RunModeText => GlobalModsEnabled ? "模组模式 · -NoBattlEye" : "纯净模式 · Unturned_BE.exe";
    public string EnvironmentText => $"{_loaderText} · {(IsDxvkEnabled ? "DXVK 2.4" : "原生 D3D11")}";

    public event EventHandler? Changed;

    public void Refresh()
    {
        var path = AppSettings.UnturnedInstallPath;
        var status = _bepInEx.GetStatus(path);
        var activeId = _profiles.GetActiveProfileId();
        ProfileName = activeId is null ? "" : _profiles.GetProfiles().FirstOrDefault(p => p.Id == activeId)?.Name ?? "";
        var mods = _mods.Scan();
        EnabledPluginCount = mods.Count(p => p.IsEnabled);
        TotalPluginCount = mods.Count;
        _globalModsEnabled = _bepInEx.IsGlobalModsEnabled(path);
        IsBepInExReady = status.IsInstalled;
        IsDxvkEnabled = _dxvk.IsEnabled(path);
        _loaderText = status.IsInstalled
            ? $"BepInEx {status.InstalledVersion ?? BepInExService.SupportedVersion} 就绪"
            : "BepInEx 未安装";
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
