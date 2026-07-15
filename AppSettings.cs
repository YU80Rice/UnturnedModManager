using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace UnturnedModManager;

public static class AppSettings
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "config.json");

    private static ConfigData _data = new();

    public static string UnturnedInstallPath
    {
        get => _data.UnturnedInstallPath;
        set { _data.UnturnedInstallPath = value; Save(); }
    }

    /// <summary>
    /// 上次游戏会话是否以异常退出（ExitCode != 0）收场。
    /// true 时主页顶部会展示崩溃提示条，引导玩家导出日志。
    /// </summary>
    public static bool LastSessionCrashed
    {
        get => _data.LastSessionCrashed;
        set { _data.LastSessionCrashed = value; Save(); }
    }

    /// <summary>
    /// 是否启用 DXVK 极速优化（将 DX11 翻译为 Vulkan 运行）。
    /// true 时会在游戏根目录部署 d3d11.dll + dxgi.dll，false 时移除。
    /// </summary>
    public static bool EnableDxvk
    {
        get => _data.EnableDxvk;
        set { _data.EnableDxvk = value; Save(); }
    }

    /// <summary>
    /// GPU 检测得到的 DXVK 推荐状态（null=未检测，true=推荐，false=不推荐）。
    /// 首次启动时由 GpuDetector 检测后写入，用于决定 DXVK 开关的初始状态。
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

    static AppSettings()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _data = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
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
        try
        {
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private class ConfigData
    {
        public string UnturnedInstallPath { get; set; } = string.Empty;

        public bool LastSessionCrashed { get; set; } = false;

        public bool EnableDxvk { get; set; } = false;

        public bool? DxvkRecommendedByGpu { get; set; } = null;

        public bool HasShownDxvkCompatWarning { get; set; } = false;

        public string ThemeMode { get; set; } = "Dark";
    }
}