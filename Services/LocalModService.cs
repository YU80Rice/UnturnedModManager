using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

/// <summary>
/// 本地插件文件系统的唯一入口。页面和 ViewModel 不再自行拼接路径或直接移动文件。
/// </summary>
public sealed class LocalModService
{
    private readonly CommunityModInstaller _installer;
    private readonly Func<string?> _gamePathProvider;

    public LocalModService(CommunityModInstaller installer)
        : this(installer, () => AppSettings.UnturnedInstallPath)
    {
    }

    public LocalModService(CommunityModInstaller installer, Func<string?> gamePathProvider)
    {
        _installer = installer;
        _gamePathProvider = gamePathProvider;
    }

    public string? GetPluginsPath()
    {
        var gamePath = _gamePathProvider();
        if (string.IsNullOrWhiteSpace(gamePath)) return null;
        return Path.Combine(gamePath, "BepInEx", "plugins");
    }

    public IReadOnlyList<ModItem> Scan()
    {
        var gamePath = _gamePathProvider();
        if (string.IsNullOrWhiteSpace(gamePath)) return [];
        _installer.Reconcile(gamePath);
        var pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
        if (!Directory.Exists(pluginsPath)) return [];

        var candidates = Directory.EnumerateFiles(pluginsPath, "*", SearchOption.AllDirectories)
            .Where(IsPluginFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 同时存在 Foo.dll 和 Foo.dll.disabled 时，以已启用文件为准，避免重复条目和含糊的开关状态。
        var enabledFiles = candidates
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return candidates
            .Where(path => !path.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase)
                || !enabledFiles.Contains(Path.GetFullPath(path[..^".disabled".Length])))
            .Select(path => CreateItem(path, pluginsPath, gamePath))
            .ToList();
    }

    public LocalModOperationResult SetEnabled(ModItem item, bool enabled)
    {
        try
        {
            var enabledPath = item.FullPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? item.FullPath[..^".disabled".Length]
                : item.FullPath;
            var disabledPath = enabledPath + ".disabled";
            var source = enabled ? disabledPath : enabledPath;
            var destination = enabled ? enabledPath : disabledPath;

            if (!File.Exists(source))
            {
                var currentStateMatches = enabled ? File.Exists(enabledPath) : File.Exists(disabledPath);
                return currentStateMatches
                    ? new(true, enabled ? "插件已启用。" : "插件已停用。")
                    : new(false, $"找不到插件文件：{item.FileName}");
            }

            if (File.Exists(destination))
                return new(false, $"无法切换状态：目标文件已存在（{Path.GetFileName(destination)}）。");

            File.Move(source, destination);
            item.FullPath = destination;
            item.IsEnabled = enabled;
            return new(true, enabled ? $"已启用 {item.DisplayTitle}。" : $"已停用 {item.DisplayTitle}。");
        }
        catch (Exception ex)
        {
            return new(false, $"无法更改插件状态：{ex.Message}");
        }
    }

    /// <summary>
    /// 在验证全部目标后批量切换本地插件状态。任何移动失败都会按相反顺序回滚，
    /// 因此插件方案不会留下半完成的启停状态。
    /// </summary>
    public LocalModBatchOperationResult ApplyStates(IReadOnlyDictionary<string, bool> desiredStates)
    {
        var items = Scan();
        var changes = new List<(ModItem Item, string Source, string Destination, bool Enabled)>();

        foreach (var item in items)
        {
            var enabled = desiredStates.TryGetValue(item.RelativePath, out var requested) && requested;
            if (item.IsEnabled == enabled) continue;

            var enabledPath = item.FullPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? item.FullPath[..^".disabled".Length]
                : item.FullPath;
            var source = enabled ? enabledPath + ".disabled" : enabledPath;
            var destination = enabled ? enabledPath : enabledPath + ".disabled";

            if (!File.Exists(source))
                return new(false, 0, $"找不到插件文件：{item.FileName}");
            if (File.Exists(destination))
                return new(false, 0, $"无法切换“{item.FileName}”：目标文件已存在（{Path.GetFileName(destination)}）。");

            changes.Add((item, source, destination, enabled));
        }

        var completed = new List<(ModItem Item, string Source, string Destination, bool Enabled)>();
        try
        {
            foreach (var change in changes)
            {
                File.Move(change.Source, change.Destination);
                completed.Add(change);
                change.Item.FullPath = change.Destination;
                change.Item.IsEnabled = change.Enabled;
            }

            return new(true, completed.Count, completed.Count == 0
                ? "当前插件状态已经符合该方案。"
                : $"已切换 {completed.Count} 个插件状态。");
        }
        catch (Exception ex)
        {
            foreach (var change in completed.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(change.Destination) && !File.Exists(change.Source))
                        File.Move(change.Destination, change.Source);
                    change.Item.FullPath = change.Source;
                    change.Item.IsEnabled = !change.Enabled;
                }
                catch { /* 保留原始错误信息；下次刷新时可显示实际磁盘状态。 */ }
            }

            return new(false, 0, $"切换插件方案失败，已尝试回滚：{ex.Message}");
        }
    }

    public LocalModOperationResult UninstallManual(ModItem item)
    {
        try
        {
            var enabledPath = item.FullPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? item.FullPath[..^".disabled".Length]
                : item.FullPath;
            var disabledPath = enabledPath + ".disabled";
            var removed = false;
            if (File.Exists(enabledPath)) { File.Delete(enabledPath); removed = true; }
            if (File.Exists(disabledPath)) { File.Delete(disabledPath); removed = true; }
            return removed
                ? new(true, $"已卸载 {item.DisplayTitle}。")
                : new(false, "插件文件已经不存在，列表将自动刷新。");
        }
        catch (Exception ex)
        {
            return new(false, $"卸载失败：{ex.Message}");
        }
    }

    public LocalModImportResult Import(IEnumerable<string> sourceFiles)
    {
        var pluginsPath = GetPluginsPath();
        if (pluginsPath is null)
            return new(0, 0, "请先在设置中选择有效的 Unturned 游戏目录。");

        Directory.CreateDirectory(pluginsPath);
        var imported = 0;
        var skipped = 0;
        var failures = new List<string>();

        foreach (var source in sourceFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(source) || !IsPluginFile(source))
            {
                skipped++;
                continue;
            }

            try
            {
                File.Copy(source, Path.Combine(pluginsPath, Path.GetFileName(source)), overwrite: true);
                imported++;
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(source)}：{ex.Message}");
            }
        }

        var message = imported > 0
            ? $"已导入 {imported} 个插件，正在匹配社区信息。"
            : "没有识别到可导入的 .dll 或 .dll.disabled 插件文件。";
        if (skipped > 0) message += $" 已跳过 {skipped} 个不支持的文件。";
        if (failures.Count > 0) message += "\n" + string.Join("\n", failures.Take(3));
        return new(imported, skipped + failures.Count, message);
    }

    public LocalModOperationResult OpenPluginsFolder()
    {
        var path = GetPluginsPath();
        if (path is null) return new(false, "请先在设置中选择 Unturned 游戏目录。");
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            return new(true, "");
        }
        catch (Exception ex)
        {
            return new(false, $"无法打开插件目录：{ex.Message}");
        }
    }

    public string GetFingerprint()
    {
        var path = GetPluginsPath();
        if (path is null || !Directory.Exists(path)) return "missing";
        try
        {
            return string.Join('|', Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(IsPluginFile)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .Select(file =>
                {
                    var info = new FileInfo(file);
                    return $"{Path.GetRelativePath(path, file)}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
                }));
        }
        catch { return Guid.NewGuid().ToString(); }
    }

    private ModItem CreateItem(string path, string pluginsPath, string gamePath)
    {
        var disabled = path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
        var enabledPath = disabled ? path[..^".disabled".Length] : path;
        var fileName = Path.GetFileName(enabledPath);
        var gameRelativePath = Path.GetRelativePath(gamePath, enabledPath);
        var owner = _installer.FindOwner(gameRelativePath);
        var assembly = ReadAssembly(path);

        return new ModItem
        {
            AssemblyName = assembly.Name ?? Path.GetFileNameWithoutExtension(fileName),
            FileName = fileName,
            RelativePath = Path.GetRelativePath(pluginsPath, enabledPath),
            FullPath = path,
            IsEnabled = !disabled,
            InstallTime = $"安装时间：{File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}",
            InstalledVersion = owner?.Version ?? assembly.Version?.ToString() ?? "",
            CommunityModId = owner?.RemoteId,
            CommunityTitle = owner?.Title ?? "",
            IsCommunityManaged = owner is not null
        };
    }

    private static AssemblyName ReadAssembly(string path)
    {
        try { return AssemblyName.GetAssemblyName(path); }
        catch
        {
            var name = Path.GetFileNameWithoutExtension(path.Replace(".disabled", "", StringComparison.OrdinalIgnoreCase));
            return new AssemblyName(name);
        }
    }

    private static bool IsPluginFile(string path) =>
        path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".dll.disabled", StringComparison.OrdinalIgnoreCase);
}

public sealed record LocalModOperationResult(bool Success, string Message);
public sealed record LocalModImportResult(int Imported, int Skipped, string Message);
public sealed record LocalModBatchOperationResult(bool Success, int Changed, string Message);
