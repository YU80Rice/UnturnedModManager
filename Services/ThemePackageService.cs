using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

/// <summary>
/// .ummtheme 主题包导出与安全沙箱导入服务。
/// </summary>
public sealed class ThemePackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    private static readonly HashSet<string> BlockedPackageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".reg", ".com", ".scr", ".msi", ".jar", ".sh"
    };

    private readonly string _storageRoot;

    public ThemePackageService()
        : this(Path.Combine(AppDataPaths.RootDirectory, "themes"))
    {
    }

    public ThemePackageService(string storageRoot)
    {
        _storageRoot = storageRoot;
    }

    public IReadOnlyList<CustomTheme> GetInstalledThemes()
    {
        if (!Directory.Exists(_storageRoot)) return [];

        var list = new List<CustomTheme>();
        foreach (var dir in Directory.EnumerateDirectories(_storageRoot))
        {
            var manifestPath = Path.Combine(dir, "theme.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var json = File.ReadAllText(manifestPath);
                    var theme = JsonSerializer.Deserialize<CustomTheme>(json);
                    if (theme is not null && theme.Validate().IsValid)
                    {
                        list.Add(theme);
                    }
                }
                catch { }
            }
        }

        return list.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public ThemePackageOperationResult ExportPackage(CustomTheme theme, string? wallpaperSourcePath, string outputPackagePath)
    {
        var validation = theme.Validate();
        if (!validation.IsValid) return new(false, validation.Message);

        var destinationDir = Path.GetDirectoryName(outputPackagePath);
        if (!string.IsNullOrWhiteSpace(destinationDir)) Directory.CreateDirectory(destinationDir);

        var tempPackage = outputPackagePath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            using (var zip = ZipFile.Open(tempPackage, ZipArchiveMode.Create))
            {
                if (!string.IsNullOrWhiteSpace(wallpaperSourcePath) && File.Exists(wallpaperSourcePath))
                {
                    var ext = Path.GetExtension(wallpaperSourcePath);
                    if (AllowedImageExtensions.Contains(ext))
                    {
                        var entryName = "wallpaper" + ext.ToLowerInvariant();
                        theme.BackgroundAsset = entryName;
                        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                        using var imgStream = File.OpenRead(wallpaperSourcePath);
                        using var entryStream = entry.Open();
                        imgStream.CopyTo(entryStream);
                    }
                }

                var manifestEntry = zip.CreateEntry("theme.json", CompressionLevel.Optimal);
                using var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8);
                writer.Write(JsonSerializer.Serialize(theme, JsonOptions));
            }

            File.Move(tempPackage, outputPackagePath, overwrite: true);
            return new(true, $"已导出主题包“{theme.Name}”至：{outputPackagePath}", theme);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPackage)) File.Delete(tempPackage);
            return new(false, $"导出主题包失败：{ex.Message}");
        }
    }

    public ThemePackageOperationResult ImportPackage(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            return new(false, "主题包文件不存在。");

        var fileInfo = new FileInfo(packagePath);
        if (fileInfo.Length > 50L * 1024 * 1024)
            return new(false, "主题包体积超出限制（50MB）。");

        try
        {
            using var zip = ZipFile.OpenRead(packagePath);
            var manifestEntry = zip.GetEntry("theme.json");
            if (manifestEntry is null)
                return new(false, "无效的主题包：未找到 theme.json 清单。");

            CustomTheme theme;
            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            {
                theme = JsonSerializer.Deserialize<CustomTheme>(reader.ReadToEnd())
                    ?? throw new InvalidDataException("无法解析 theme.json。");
            }

            var validation = theme.Validate();
            if (!validation.IsValid) return new(false, validation.Message);

            // 严格沙箱校验条目
            string? wallpaperEntryName = null;
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (normalized.Equals("theme.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var ext = Path.GetExtension(normalized);
                if (BlockedPackageExtensions.Contains(ext))
                    return new(false, $"主题包包含危险的载荷（{ext}），已阻止导入。");

                if (!AllowedImageExtensions.Contains(ext))
                    return new(false, $"主题包包含不支持的文件类型（{normalized}），仅允许包含背景图片。");

                if (normalized.Contains('/') || normalized.Contains('\\') || normalized.Contains(".."))
                    return new(false, "主题包包含非法的嵌套或相对路径。");

                wallpaperEntryName = entry.FullName;
            }

            // 安全解压至主题独立目录
            var themeDir = Path.Combine(_storageRoot, theme.Id);
            Directory.CreateDirectory(themeDir);

            var manifestDest = Path.Combine(themeDir, "theme.json");
            File.WriteAllText(manifestDest, JsonSerializer.Serialize(theme, JsonOptions));

            string? destWallpaper = null;
            if (!string.IsNullOrWhiteSpace(wallpaperEntryName))
            {
                var entry = zip.GetEntry(wallpaperEntryName);
                if (entry is not null)
                {
                    destWallpaper = Path.Combine(themeDir, wallpaperEntryName);
                    using var sourceStream = entry.Open();
                    using var destStream = File.Create(destWallpaper);
                    sourceStream.CopyTo(destStream);
                }
            }

            return new(true, $"已成功导入主题“{theme.Name}”。", theme, destWallpaper);
        }
        catch (Exception ex)
        {
            return new(false, $"导入主题包失败：{ex.Message}");
        }
    }

    public bool DeleteTheme(string themeId)
    {
        try
        {
            var themeDir = Path.Combine(_storageRoot, themeId);
            if (Directory.Exists(themeDir))
            {
                Directory.Delete(themeDir, true);
                return true;
            }
        }
        catch { }
        return false;
    }
}
