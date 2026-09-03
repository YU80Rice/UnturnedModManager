using Microsoft.Win32;
using System.IO;

namespace UnturnedModManager.Services;

/// <summary>
/// 管理 Windows 资源管理器中 .ummpk（模组包）与 .ummtheme（主题包）的文件类型关联与打开方式。
/// 遵循 ADR-0004 规范：便携隔离模式下保持静默，非隔离常规运行时自愈注册，并支持在设置中手动修复与解除关联。
/// </summary>
public static class ShellAssociationService
{
    public const string ModPackageExtension = ".ummpk";
    public const string ThemePackageExtension = ".ummtheme";
    public const string ModPackageProgId = "UMM.ModPackage";
    public const string ThemePackageProgId = "UMM.ThemePackage";
    public const string ModPackageDocName = "Unturned Mod Manager Package";
    public const string ThemePackageDocName = "Unturned Mod Manager Theme Package";

    public static string BuildOpenCommandLine(string executablePath)
    {
        return $"\"{executablePath}\" \"%1\"";
    }

    public static bool ShouldSkipRegistration(bool isWindows, bool isIsolated, string? executablePath)
    {
        if (!isWindows) return true;
        if (isIsolated) return true;
        if (string.IsNullOrWhiteSpace(executablePath)) return true;
        if (Path.GetFileNameWithoutExtension(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool EnsureRegistered()
    {
        var executable = Environment.ProcessPath;
        if (ShouldSkipRegistration(OperatingSystem.IsWindows(), AppDataPaths.IsIsolatedProfile, executable))
            return false;

        return RegisterAssociations();
    }

    public static bool RegisterAssociations()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            return false;

        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var command = BuildOpenCommandLine(executable);
            var icon = $"\"{executable}\",0";

            // 1. 注册 .ummpk
            using (var extKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Classes\\{ModPackageExtension}"))
            {
                extKey.SetValue(null, ModPackageProgId);
                extKey.SetValue("Content Type", "application/x-unturned-mod-package");
            }

            using (var progKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Classes\\{ModPackageProgId}"))
            {
                progKey.SetValue(null, ModPackageDocName);
                progKey.SetValue("FriendlyTypeName", ModPackageDocName);
                using (var iconKey = progKey.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue(null, icon);
                }
                using (var cmdKey = progKey.CreateSubKey("shell\\open\\command"))
                {
                    cmdKey.SetValue(null, command);
                }
            }

            // 2. 注册 .ummtheme
            using (var extKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Classes\\{ThemePackageExtension}"))
            {
                extKey.SetValue(null, ThemePackageProgId);
                extKey.SetValue("Content Type", "application/x-unturned-theme-package");
            }

            using (var progKey = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Classes\\{ThemePackageProgId}"))
            {
                progKey.SetValue(null, ThemePackageDocName);
                progKey.SetValue("FriendlyTypeName", ThemePackageDocName);
                using (var iconKey = progKey.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue(null, icon);
                }
                using (var cmdKey = progKey.CreateSubKey("shell\\open\\command"))
                {
                    cmdKey.SetValue(null, command);
                }
            }

            ProtocolRegistrar.EnsureRegistered();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool UnregisterAssociations()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var classes = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Classes", writable: true);
            if (classes is null)
                return false;

            TryDeleteSubKeyTree(classes, ModPackageExtension);
            TryDeleteSubKeyTree(classes, ModPackageProgId);
            TryDeleteSubKeyTree(classes, ThemePackageExtension);
            TryDeleteSubKeyTree(classes, ThemePackageProgId);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
                return false;

            using var extKey = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Classes\\{ModPackageExtension}");
            if (extKey?.GetValue(null) as string != ModPackageProgId)
                return false;

            using var cmdKey = Registry.CurrentUser.OpenSubKey($"SOFTWARE\\Classes\\{ModPackageProgId}\\shell\\open\\command");
            var currentCommand = cmdKey?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(currentCommand))
                return false;

            return currentCommand.Contains(executable, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseFileIntent(string? argument, out ShellFileIntent? intent)
    {
        intent = null;
        if (string.IsNullOrWhiteSpace(argument))
            return false;

        var raw = argument.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        ShellFileIntentType type;
        if (raw.EndsWith(ModPackageExtension, StringComparison.OrdinalIgnoreCase))
        {
            type = ShellFileIntentType.ModPackage;
        }
        else if (raw.EndsWith(ThemePackageExtension, StringComparison.OrdinalIgnoreCase))
        {
            type = ShellFileIntentType.ThemePackage;
        }
        else
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(raw);
            if (!File.Exists(fullPath))
                return false;

            intent = new ShellFileIntent(fullPath, type);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static ShellFileIntent? FindFileIntent(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument))
                continue;

            if (string.Equals(argument, "--import", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "-i", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "/open", StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryParseFileIntent(argument, out var intent))
                return intent;
        }

        return null;
    }

    private static void TryDeleteSubKeyTree(RegistryKey parent, string subKeyName)
    {
        try
        {
            parent.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
        }
        catch { }
    }
}

public enum ShellFileIntentType
{
    ModPackage,
    ThemePackage
}

public sealed record ShellFileIntent(string FilePath, ShellFileIntentType Type);

/// <summary>
/// 负责外部文件关联唤醒的 FIFO 任务队列与单一模态守护（Single Modal Guard）。
/// 确保外部多次连续双击唤醒时，向导窗口依序展示，绝不出现弹窗重叠死锁或并发竞争。
/// </summary>
public sealed class ShellIntentQueue
{
    private readonly Queue<ShellFileIntent> _queue = new();
    private bool _isProcessing;

    public int Count
    {
        get { lock (_queue) return _queue.Count; }
    }

    public bool IsProcessing
    {
        get { lock (_queue) return _isProcessing; }
    }

    public void Enqueue(ShellFileIntent intent)
    {
        lock (_queue)
        {
            _queue.Enqueue(intent);
        }
    }

    public bool TryStartNext(out ShellFileIntent? intent)
    {
        lock (_queue)
        {
            if (_isProcessing || _queue.Count == 0)
            {
                intent = null;
                return false;
            }

            _isProcessing = true;
            intent = _queue.Dequeue();
            return true;
        }
    }

    public void FinishCurrent()
    {
        lock (_queue)
        {
            _isProcessing = false;
        }
    }
}

