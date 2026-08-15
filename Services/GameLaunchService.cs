using System.Diagnostics;
using System.IO;

namespace UnturnedModManager.Services;

public sealed class GameLaunchService
{
    private readonly BepInExService _bepInEx;
    private readonly DxvkService _dxvk;
    private readonly object _processLock = new();
    // 保持对子进程的引用，确保 UMM 运行期间 Exited 事件不会因 Process 被 GC 而丢失。
    private Process? _launchedProcess;

    public GameLaunchService(BepInExService bepInEx, DxvkService dxvk)
    {
        _bepInEx = bepInEx;
        _dxvk = dxvk;
    }

    public bool IsRunning() => IsUnturnedRunning();

    public static bool IsUnturnedRunning() =>
        Process.GetProcessesByName("Unturned").Length > 0
        || Process.GetProcessesByName("Unturned_BE").Length > 0;

    public LocalModOperationResult Launch(string gamePath, bool modsEnabled)
    {
        if (!File.Exists(Path.Combine(gamePath, "Unturned.exe")))
            return new(false, "Unturned 游戏路径无效，请在设置中重新选择。");
        if (IsRunning()) return new(false, "检测到 Unturned 已在运行，请勿重复启动。");
        try
        {
            _bepInEx.EnsureModFileState(gamePath, modsEnabled);
            EnsureSteamAppId(gamePath);
            var dxvkEnabled = _dxvk.IsEnabled(gamePath);
            if (dxvkEnabled) _dxvk.EnsureConfiguration(gamePath);

            var executable = Path.Combine(gamePath, modsEnabled ? "Unturned.exe" : "Unturned_BE.exe");
            if (!File.Exists(executable))
                return new(false, modsEnabled
                    ? $"未找到游戏主程序：{executable}"
                    : $"未找到 BattlEye 启动程序：{executable}");

            var startInfo = new ProcessStartInfo(executable, modsEnabled ? "-NoBattlEye" : "")
            {
                UseShellExecute = false,
                WorkingDirectory = gamePath
            };
            startInfo.EnvironmentVariables["SteamAppId"] = "304930";
            startInfo.EnvironmentVariables["SteamGameId"] = "304930";
            startInfo.EnvironmentVariables["SteamOverlayGameId"] = "304930";
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Windows 未能创建游戏进程。");
            AppSettings.LastSessionCrashed = false;
            AppSettings.LastSessionExitCode = null;
            AppSettings.LastSessionUsedMods = modsEnabled;
            AppSettings.LastSessionUsedDxvk = dxvkEnabled;
            AppSettings.LastSessionEndedUtc = null;
            process.Exited += (_, _) =>
            {
                try
                {
                    AppSettings.LastSessionExitCode = process.ExitCode;
                    AppSettings.LastSessionCrashed = process.ExitCode != 0;
                    AppSettings.LastSessionEndedUtc = DateTime.UtcNow;
                }
                catch { }
                finally
                {
                    lock (_processLock)
                    {
                        if (ReferenceEquals(_launchedProcess, process))
                            _launchedProcess = null;
                    }
                    process.Dispose();
                }
            };
            lock (_processLock) _launchedProcess = process;
            // 订阅完成后再开启事件，避免极快退出的进程在回调注册前被漏记。
            process.EnableRaisingEvents = true;

            var mode = modsEnabled ? "模组模式 · 已跳过 BattlEye" : "纯净模式 · BattlEye 已启用";
            return new(true, $"正在启动游戏（{mode}{(dxvkEnabled ? " · DXVK" : "")}）…");
        }
        catch (Exception ex) { return new(false, $"无法启动游戏：{ex.Message}"); }
    }

    private static void EnsureSteamAppId(string gamePath)
    {
        var path = Path.Combine(gamePath, "steam_appid.txt");
        if (!File.Exists(path) || File.ReadAllText(path).Trim() != "304930")
            File.WriteAllText(path, "304930\n");
    }
}
