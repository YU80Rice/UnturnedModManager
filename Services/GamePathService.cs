using System.IO;

namespace UnturnedModManager.Services;

public interface IFolderPickerService
{
    string? PickFolder(string? initialPath, string description);
}

public sealed class WindowsFolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialPath, string description)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = description,
            SelectedPath = initialPath ?? ""
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}

public sealed class GamePathService
{
    public bool IsValid(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && Directory.Exists(path)
        && File.Exists(Path.Combine(path, "Unturned.exe"));

    public Task<string?> DetectAsync() => Task.Run(AppSettings.DetectSteamUnturnedPath);
}
