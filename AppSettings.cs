using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using UnturnedModManager.Services;

namespace UnturnedModManager;

public static class AppSettings
{
    private static readonly object SaveLock = new();
    private static readonly string ConfigDirectory = AppDataPaths.RootDirectory;
    private static readonly string ConfigPath = Path.Combine(
        ConfigDirectory,
        "config.json");
    private static readonly string? LegacyConfigPath = AppDataPaths.IsIsolatedProfile
        ? null
        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static ConfigData _data = new();

    public static string UnturnedInstallPath
    {
        get => _data.UnturnedInstallPath;
        set { _data.UnturnedInstallPath = value; Save(); }
    }

    /// <summary>
    /// 上次由 UMM 启动的游戏会话是否以非零退出码收场。
    /// 这只是异常退出线索，不等同于已确认的游戏崩溃；true 时首页会引导玩家分析日志。
    /// </summary>
    public static bool LastSessionCrashed
    {
        get => _data.LastSessionCrashed;
        set { _data.LastSessionCrashed = value; Save(); }
    }

    /// <summary>
    /// 是否启用 DXVK 渲染测试（将 DX11 翻译为 Vulkan 运行）。
    /// true 时会在游戏根目录部署 d3d11.dll + dxgi.dll，false 时停用为 .disabled。
    /// </summary>
    public static bool EnableDxvk
    {
        get => _data.EnableDxvk;
        set { _data.EnableDxvk = value; Save(); }
    }

    /// <summary>
    /// GPU 检测得到的 DXVK 测试建议（null=未检测，true=可测试，false=不建议）。
    /// 首次启动时由 GpuDetector 检测后写入，用于决定是否显示兼容性确认。
    /// </summary>
    public static bool? DxvkRecommendedByGpu
    {
        get => _data.DxvkRecommendedByGpu;
        set { _data.DxvkRecommendedByGpu = value; Save(); }
    }

    /// <summary>
    /// 是否已展示过 DXVK 兼容性警告（避免每次启动重复弹窗）。
    /// 仅当用户手动开启 DXVK 但 GPU 不推荐时才弹窗，弹过一次后置 true。
    /// </summary>
    public static bool HasShownDxvkCompatWarning
    {
        get => _data.HasShownDxvkCompatWarning;
        set { _data.HasShownDxvkCompatWarning = value; Save(); }
    }

    /// <summary>
    /// 应用主题模式："Dark"（深色，默认）或 "Light"（浅色）。
    /// 启动时由 MainWindow 应用，运行时通过主题切换按钮即时切换。
    /// </summary>
    public static string ThemeMode
    {
        get => string.IsNullOrEmpty(_data.ThemeMode) ? "Dark" : _data.ThemeMode;
        set { _data.ThemeMode = value; Save(); }
    }

    public static string CommunityThemeMode
    {
        get => string.IsNullOrEmpty(_data.CommunityThemeMode) ? "System" : _data.CommunityThemeMode;
        set { _data.CommunityThemeMode = value; Save(); }
    }

    /// <summary>
    /// 独立于明暗模式的界面配色方案。保留 Fluent 默认方案，并提供暖米白方案。
    /// </summary>
    public static string CommunityColorPalette
    {
        get => string.IsNullOrEmpty(_data.CommunityColorPalette) ? "Fluent" : _data.CommunityColorPalette;
        set { _data.CommunityColorPalette = value; Save(); }
    }

    public static int? LastSessionExitCode
    {
        get => _data.LastSessionExitCode;
        set { _data.LastSessionExitCode = value; Save(); }
    }

    public static bool LastSessionUsedMods
    {
        get => _data.LastSessionUsedMods;
        set { _data.LastSessionUsedMods = value; Save(); }
    }

    public static bool LastSessionUsedDxvk
    {
        get => _data.LastSessionUsedDxvk;
        set { _data.LastSessionUsedDxvk = value; Save(); }
    }

    public static DateTime? LastSessionEndedUtc
    {
        get => _data.LastSessionEndedUtc;
        set { _data.LastSessionEndedUtc = value; Save(); }
    }

    /// <summary>
    /// 用于避免沿用虚拟显示适配器得出的旧 DXVK 判断。
    /// </summary>
    public static string? DxvkRecommendationGpuName
    {
        get => _data.DxvkRecommendationGpuName;
        set { _data.DxvkRecommendationGpuName = value; Save(); }
    }

    public static string? CommunityAuthToken
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_data.CommunityAuthTokenProtected))
            {
                try
                {
                    var encrypted = Convert.FromBase64String(_data.CommunityAuthTokenProtected);
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser));
                }
                catch { }
            }
            return _data.CommunityAuthToken;
        }
        set
        {
            _data.CommunityAuthToken = null;
            _data.CommunityAuthTokenProtected = string.IsNullOrWhiteSpace(value)
                ? null
                : Convert.ToBase64String(ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
            Save();
        }
    }
    public static int? CommunityUserId { get => _data.CommunityUserId; set { _data.CommunityUserId = value; Save(); } }
    public static string? CommunityUsername { get => _data.CommunityUsername; set { _data.CommunityUsername = value; Save(); } }
    public static string? CommunityAvatarUrl { get => _data.CommunityAvatarUrl; set { _data.CommunityAvatarUrl = value; Save(); } }

    public static bool IsNavigationPaneOpen
    {
        get => _data.IsNavigationPaneOpen;
        set { _data.IsNavigationPaneOpen = value; Save(); }
    }
    public static bool IsOnboardingCompleted
    {
        get => _data.IsOnboardingCompleted;
        set { _data.IsOnboardingCompleted = value; Save(); }
    }

    /// <summary>首页是否展示可选的吉祥物欢迎区与版本公告。</summary>
    public static bool IsHomeWelcomeEnabled
    {
        get => _data.IsHomeWelcomeEnabled;
        set { _data.IsHomeWelcomeEnabled = value; Save(); }
    }

    /// <summary>
    /// 最近已确认过的首页公告版本。程序版本变更时，欢迎区会再次展示对应版本的更新摘要；
    /// 这是本地版本提示，不会在后台联网读取或执行自更新。
    /// </summary>
    public static string? LastAcknowledgedHomeAnnouncementVersion
    {
        get => _data.LastAcknowledgedHomeAnnouncementVersion;
        set { _data.LastAcknowledgedHomeAnnouncementVersion = value; Save(); }
    }

    public static string CurrentHomeAnnouncementVersion =>
        typeof(AppSettings).Assembly.GetName().Version?.ToString(3) ?? "2.1.1";
    public static double WindowWidth { get => _data.WindowWidth; set { _data.WindowWidth = value; Save(); } }
    public static double WindowHeight { get => _data.WindowHeight; set { _data.WindowHeight = value; Save(); } }
    // WPF 用 NaN 表示“没有保存的位置”。JSON 不支持 NaN，因此持久化层使用 null，
    // 这样全新用户在第一次完成引导时也能立即保存配置。
    public static double WindowLeft
    {
        get => _data.WindowLeft ?? double.NaN;
        set { _data.WindowLeft = double.IsFinite(value) ? value : null; Save(); }
    }
    public static double WindowTop
    {
        get => _data.WindowTop ?? double.NaN;
        set { _data.WindowTop = double.IsFinite(value) ? value : null; Save(); }
    }
    public static bool IsWindowMaximized { get => _data.IsWindowMaximized; set { _data.IsWindowMaximized = value; Save(); } }

    static AppSettings()
    {
        try
        {
            var sourcePath = File.Exists(ConfigPath)
                ? ConfigPath
                : LegacyConfigPath is { } legacy && File.Exists(legacy) ? legacy : null;
            if (sourcePath is not null)
            {
                var json = File.ReadAllText(sourcePath);
                _data = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
                if (string.IsNullOrWhiteSpace(_data.CommunityAuthTokenProtected)
                    && !string.IsNullOrWhiteSpace(_data.CommunityAuthToken))
                {
                    var legacyToken = _data.CommunityAuthToken;
                    _data.CommunityAuthToken = null;
                    _data.CommunityAuthTokenProtected = Convert.ToBase64String(ProtectedData.Protect(
                        Encoding.UTF8.GetBytes(legacyToken), null, DataProtectionScope.CurrentUser));
                    Save();
                }
                else if (!string.Equals(sourcePath, ConfigPath, StringComparison.OrdinalIgnoreCase)) Save();

                // Existing UMM installations already have a configured game path;
                // do not interrupt them with the first-run wizard after upgrading.
                if (!_data.IsOnboardingCompleted && !string.IsNullOrWhiteSpace(_data.UnturnedInstallPath))
                {
                    _data.IsOnboardingCompleted = true;
                    Save();
                }
            }
        }
        catch { }
        // 注意：首次启动的注册表主动探测改由 HomePage_Loaded 触发并经用户确认后再写入，
        // 避免静默写入绕过用户感知。
    }

    /// <summary>
    /// 通过 Windows 注册表 + Steam 库配置（libraryfolders.vdf）扫描 Unturned 安装路径。
    /// 完整 fallback 链：
    /// 1) 注册表 Steam App 304930 的 InstallLocation（部分 Steam 版本会留空）
    /// 2) 从 UninstallString 提取 steam.exe 路径 -> 推导 Steam 根目录
    /// 3) 读取 Steam\steamapps\libraryfolders.vdf 获取所有 Steam 库
    /// 4) 在每个库的 steamapps\common\Unturned 下校验 Unturned.exe 是否存在
    /// 兼容 32/64 位 Windows、HKLM/HKCU、单库/多库 Steam 安装。
    /// </summary>
    public static string? DetectSteamUnturnedPath()
    {
        const string uninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 304930";

        // 步骤 1：先尝试 InstallLocation 快速路径
        var installLocation = ReadRegistryValue(uninstallKeyPath, "InstallLocation");
        if (IsUnturnedInstall(installLocation))
            return installLocation;

        // 步骤 2：从 UninstallString 提取 steam.exe 路径
        var uninstallString = ReadRegistryValue(uninstallKeyPath, "UninstallString");
        var steamExePath = ExtractExecutablePath(uninstallString);
        if (string.IsNullOrEmpty(steamExePath) || !File.Exists(steamExePath))
            return null;

        var steamRoot = Path.GetDirectoryName(steamExePath);
        if (string.IsNullOrEmpty(steamRoot))
            return null;

        // 步骤 3+4：遍历 Steam 库目录寻找 Unturned
        foreach (var libraryPath in EnumerateSteamLibraries(steamRoot))
        {
            var unturnedPath = Path.Combine(libraryPath, "steamapps", "common", "Unturned");
            if (IsUnturnedInstall(unturnedPath))
                return unturnedPath;
        }

        return null;
    }

    /// <summary>
    /// 在指定 Steam 根目录下读取 libraryfolders.vdf，返回所有 Steam 库路径。
    /// Steam 根目录本身也是一个库（默认库），会被作为第一项返回。
    /// </summary>
    private static List<string> EnumerateSteamLibraries(string steamRoot)
    {
        var libraries = new List<string> { steamRoot };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };

        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath))
            return libraries;

        try
        {
            var content = File.ReadAllText(vdfPath);
            // vdf 中库路径形如:  "path"     "E:\\Steam"
            // 用正则提取，注意 vdf 中反斜杠被转义为 \\
            var matches = Regex.Matches(content, @"""path""\s+""([^""]+)""");
            foreach (Match match in matches)
            {
                var libPath = match.Groups[1].Value.Replace("\\\\", "\\");
                if (!string.IsNullOrEmpty(libPath)
                    && Directory.Exists(libPath)
                    && seen.Add(libPath))
                {
                    libraries.Add(libPath);
                }
            }
        }
        catch
        {
            // vdf 读取失败时静默降级，仅返回默认库
        }

        return libraries;
    }

    /// <summary>
    /// 从形如 "E:\Steam\steam.exe" steam://uninstall/304930 的字符串中提取可执行文件路径。
    /// </summary>
    private static string? ExtractExecutablePath(string? uninstallString)
    {
        if (string.IsNullOrEmpty(uninstallString))
            return null;

        // 优先匹配带引号的路径
        var match = Regex.Match(uninstallString, @"""([^""]+\.exe)""", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;

        // 兜底：取第一个空格前的部分
        var spaceIndex = uninstallString.IndexOf(' ');
        if (spaceIndex > 0)
        {
            var candidate = uninstallString.Substring(0, spaceIndex);
            if (File.Exists(candidate))
                return candidate;
        }

        // 整串本身就是路径
        return File.Exists(uninstallString) ? uninstallString : null;
    }

    /// <summary>
    /// 跨 Registry64/Registry32 视图与 HKLM/HKCU 根读取注册表值。
    /// </summary>
    private static string? ReadRegistryValue(string keyPath, string valueName)
    {
        var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
        var hives = new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };

        foreach (var view in views)
        {
            foreach (var hive in hives)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var subKey = baseKey.OpenSubKey(keyPath);
                    var value = subKey?.GetValue(valueName) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
                catch
                {
                    // 视图/根不存在或无权限时静默跳过
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 校验给定路径是否为有效的 Unturned 安装（路径存在且包含 Unturned.exe）。
    /// </summary>
    private static bool IsUnturnedInstall(string? path)
    {
        return !string.IsNullOrEmpty(path)
            && Directory.Exists(path)
            && File.Exists(Path.Combine(path, "Unturned.exe"));
    }

    public static void Save()
    {
        lock (SaveLock)
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigPath,
                    JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }

    private class ConfigData
    {
        public string UnturnedInstallPath { get; set; } = string.Empty;

        public bool LastSessionCrashed { get; set; } = false;

        public bool EnableDxvk { get; set; } = false;

        public bool? DxvkRecommendedByGpu { get; set; } = null;

        public bool HasShownDxvkCompatWarning { get; set; } = false;

        public string ThemeMode { get; set; } = "Dark";
        public string CommunityThemeMode { get; set; } = "System";
        public string CommunityColorPalette { get; set; } = "Fluent";
        public int? LastSessionExitCode { get; set; }
        public bool LastSessionUsedMods { get; set; }
        public bool LastSessionUsedDxvk { get; set; }
        public DateTime? LastSessionEndedUtc { get; set; }
        public string? DxvkRecommendationGpuName { get; set; }
        public bool IsNavigationPaneOpen { get; set; } = true;
        public bool IsOnboardingCompleted { get; set; }
        public bool IsHomeWelcomeEnabled { get; set; } = true;
        public string? LastAcknowledgedHomeAnnouncementVersion { get; set; }
        public double WindowWidth { get; set; } = 1280;
        public double WindowHeight { get; set; } = 820;
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }
        public bool IsWindowMaximized { get; set; }
        public string? CommunityAuthToken { get; set; }
        public string? CommunityAuthTokenProtected { get; set; }
        public int? CommunityUserId { get; set; }
        public string? CommunityUsername { get; set; }
        public string? CommunityAvatarUrl { get; set; }
    }
}
