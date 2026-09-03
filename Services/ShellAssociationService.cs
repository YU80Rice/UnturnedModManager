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

    private static void TryDeleteSubKeyTree(RegistryKey parent, string subKeyName)
    {
        try
        {
            parent.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
        }
        catch { }
    }
}
