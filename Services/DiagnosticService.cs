using System.Diagnostics;
using System.IO;

namespace UnturnedModManager.Services;

public sealed class DiagnosticService
{
    public string ExportLogs(string? gamePath)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var folder = Path.Combine(desktop, $"Unturned_模组崩溃诊断_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(folder);
        CopyIfExists(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "SmartlyDressedGames", "Unturned", "Player-prev.log"),
            Path.Combine(folder, "Player-prev.log"));
        if (!string.IsNullOrWhiteSpace(gamePath))
            CopyIfExists(Path.Combine(gamePath, "BepInEx", "LogOutput.log"), Path.Combine(folder, "LogOutput.log"));
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        return folder;
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (File.Exists(source)) File.Copy(source, destination, overwrite: true);
    }
}
