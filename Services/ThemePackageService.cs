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

    public ThemePackageOperationResult ExportPackage(CustomTheme theme, string? wallpaperSourcePath, string outputPackagePath) =>
        ExportDualModePackage(theme, wallpaperSourcePath, null, outputPackagePath);

    public ThemePackageOperationResult ExportDualModePackage(
        CustomTheme theme,
        string? wallpaperDarkSourcePath,
        string? wallpaperLightSourcePath,
        string outputPackagePath)
    {
        theme.EnsureDualMode();
        var validation = theme.Validate();
        if (!validation.IsValid) return new(false, validation.Message);

        var destinationDir = Path.GetDirectoryName(outputPackagePath);
        if (!string.IsNullOrWhiteSpace(destinationDir)) Directory.CreateDirectory(destinationDir);

        var tempPackage = outputPackagePath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            using (var zip = ZipFile.Open(tempPackage, ZipArchiveMode.Create))
            {
                // Dark or single wallpaper
                if (!string.IsNullOrWhiteSpace(wallpaperDarkSourcePath) && File.Exists(wallpaperDarkSourcePath))
                {
                    var ext = Path.GetExtension(wallpaperDarkSourcePath);
                    if (AllowedImageExtensions.Contains(ext))
                    {
                        var entryName = string.IsNullOrWhiteSpace(wallpaperLightSourcePath)
                            ? ("wallpaper" + ext.ToLowerInvariant())
                            : ("wallpaper_dark" + ext.ToLowerInvariant());
                        theme.BackgroundAsset = entryName;
                        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                        using var imgStream = File.OpenRead(wallpaperDarkSourcePath);
                        using var entryStream = entry.Open();
                        imgStream.CopyTo(entryStream);
                    }
                }

                // Light wallpaper
                if (!string.IsNullOrWhiteSpace(wallpaperLightSourcePath) && File.Exists(wallpaperLightSourcePath))
                {
                    var ext = Path.GetExtension(wallpaperLightSourcePath);
                    if (AllowedImageExtensions.Contains(ext))
                    {
                        var entryName = "wallpaper_light" + ext.ToLowerInvariant();
                        theme.LightBackgroundAsset = entryName;
                        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                        using var imgStream = File.OpenRead(wallpaperLightSourcePath);
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

    public ThemePackagePreview? InspectPackagePreview(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            return null;

        var fileInfo = new FileInfo(packagePath);
        var preview = new ThemePackagePreview
        {
            PackagePath = packagePath,
            PackageSizeBytes = fileInfo.Length
        };

        try
        {
            using var zip = ZipFile.OpenRead(packagePath);
            var manifestEntry = zip.GetEntry("theme.json");
            if (manifestEntry is null) return null;

            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            {
                preview.Theme = JsonSerializer.Deserialize<CustomTheme>(reader.ReadToEnd()) ?? new();
                preview.Theme.EnsureDualMode();
            }

            var imageEntries = new List<ZipArchiveEntry>();
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (normalized.Equals("theme.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var ext = Path.GetExtension(normalized);
                if (AllowedImageExtensions.Contains(ext))
                {
                    imageEntries.Add(entry);
                }
            }

            foreach (var entry in imageEntries)
            {
                var lowerName = entry.Name.ToLowerInvariant();
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    using var s = entry.Open();
                    s.CopyTo(ms);
                    bytes = ms.ToArray();
                }

                if (lowerName.Contains("light") || (!string.IsNullOrEmpty(preview.Theme.LightBackgroundAsset) && entry.Name.Equals(preview.Theme.LightBackgroundAsset, StringComparison.OrdinalIgnoreCase)))
                {
                    preview.WallpaperLightFileName = entry.Name;
                    preview.WallpaperLightBytes = bytes;
                }
                else if (lowerName.Contains("dark") || (!string.IsNullOrEmpty(preview.Theme.BackgroundAsset) && entry.Name.Equals(preview.Theme.BackgroundAsset, StringComparison.OrdinalIgnoreCase)))
                {
                    preview.WallpaperDarkFileName = entry.Name;
                    preview.WallpaperDarkBytes = bytes;
                }
                else
                {
                    // Generic fallback
                    if (preview.WallpaperDarkBytes is null)
                    {
                        preview.WallpaperDarkFileName = entry.Name;
                        preview.WallpaperDarkBytes = bytes;
                    }
                }
            }

            // 若仅有一张壁纸，则白天与夜间均可复用此壁纸
            if (preview.WallpaperDarkBytes is not null && preview.WallpaperLightBytes is null)
            {
                preview.WallpaperLightBytes = preview.WallpaperDarkBytes;
                preview.WallpaperLightFileName = preview.WallpaperDarkFileName;
            }

            return preview;
        }
        catch
        {
            return null;
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
                theme.EnsureDualMode();
            }

            var validation = theme.Validate();
            if (!validation.IsValid) return new(false, validation.Message);

            // 严格沙箱校验条目（仅允许合法图片且无路径穿越）
            var wallpaperEntries = new List<string>();
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

                wallpaperEntries.Add(entry.FullName);
            }

            // 安全解压至主题独立目录
            var themeDir = Path.Combine(_storageRoot, theme.Id);
            Directory.CreateDirectory(themeDir);

            var manifestDest = Path.Combine(themeDir, "theme.json");
            File.WriteAllText(manifestDest, JsonSerializer.Serialize(theme, JsonOptions));

            string? destWallpaperDark = null;
            string? destWallpaperLight = null;

            foreach (var wpEntryName in wallpaperEntries)
            {
                var entry = zip.GetEntry(wpEntryName);
                if (entry is not null)
                {
                    var destFile = Path.Combine(themeDir, wpEntryName);
                    using var sourceStream = entry.Open();
                    using var destStream = File.Create(destFile);
                    sourceStream.CopyTo(destStream);

                    var lower = wpEntryName.ToLowerInvariant();
                    if (lower.Contains("light") && !lower.Contains("dark"))
                    {
                        destWallpaperLight = destFile;
                    }
                    else
                    {
                        destWallpaperDark = destFile;
                    }
                }
            }

            destWallpaperDark ??= destWallpaperLight;
            destWallpaperLight ??= destWallpaperDark;

            return new(true, $"已成功导入主题“{theme.Name}”。", theme, destWallpaperDark, destWallpaperLight);
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
