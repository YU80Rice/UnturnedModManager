using System.IO;
using System.IO.Compression;
using System.Text;

namespace UnturnedModManager.Services;

public sealed record SandboxViolation(
    string EntryPath,
    string RuleName,
    string Description);

public sealed class SandboxInspectionResult
{
    public string PackagePath { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public long TotalBytes { get; set; }
    public List<SandboxViolation> Violations { get; } = [];

    public bool IsValid => Violations.Count == 0;

    public string FormatDiagnosticReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== UMM 安全沙箱预检诊断报告 ===");
        sb.AppendLine($"检测时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"文件路径: {PackagePath}");
        sb.AppendLine($"包类型: {PackageType}");
        sb.AppendLine($"条目数量: {EntryCount}");
        sb.AppendLine($"总解压体积: {TotalBytes:N0} 字节");
        sb.AppendLine($"安全结论: {(IsValid ? "通过 (安全)" : $"拦截 - 发现 {Violations.Count} 处沙箱安全违规")}");
        sb.AppendLine("----------------------------------------");
        if (Violations.Count > 0)
        {
            sb.AppendLine("违规条目明细:");
            for (var i = 0; i < Violations.Count; i++)
            {
                var v = Violations[i];
                sb.AppendLine($"[{i + 1}] 条目: {v.EntryPath}");
                sb.AppendLine($"    规则: {v.RuleName}");
                sb.AppendLine($"    详情: {v.Description}");
            }
        }
        else
        {
            sb.AppendLine("未发现任何违规项。符合 UMM 沙箱白名单。");
        }
        sb.AppendLine("========================================");
        return sb.ToString();
    }
}

/// <summary>
/// 提供对外部 .ummpk 与 .ummtheme 包的非绕过多层沙箱预检。
/// 严格拦截危险脚本可执行文件、路径穿越攻击、解压炸弹与越界写入。
/// </summary>
public sealed class PackageSandboxInspectionService
{
    public const int MaxArchiveEntries = 4096;
    public const long MaxExpandedBytes = 1024L * 1024 * 1024; // 1 GB
    public const long MaxThemePackageSizeBytes = 50L * 1024 * 1024; // 50 MB

    private static readonly HashSet<string> BlockedScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".reg", ".com", ".scr",
        ".msi", ".jar", ".sh", ".vbe", ".wsf", ".wsh", ".hta", ".cpl",
        ".pif", ".gadget", ".msp", ".bash", ".action"
    };

    private static readonly HashSet<string> AllowedThemeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public SandboxInspectionResult Inspect(ShellFileIntent intent) =>
        intent.Type == ShellFileIntentType.ThemePackage
            ? InspectThemePackage(intent.FilePath)
            : InspectModPackage(intent.FilePath);

    public SandboxInspectionResult InspectModPackage(string packagePath)
    {
        var result = new SandboxInspectionResult
        {
            PackagePath = packagePath,
            PackageType = "ModPackage (.ummpk)"
        };

        if (!File.Exists(packagePath))
        {
            result.Violations.Add(new SandboxViolation(packagePath, "FileNotFound", "目标模组包文件在磁盘上不存在或已被移除。"));
            return result;
        }

        try
        {
            using var zip = ZipFile.OpenRead(packagePath);
            result.EntryCount = zip.Entries.Count;

            if (zip.Entries.Count > MaxArchiveEntries)
            {
                result.Violations.Add(new SandboxViolation(
                    packagePath,
                    "ZipBomb.EntryCountExceeded",
                    $"压缩包条目总数（{zip.Entries.Count}）超过最大允许上限（{MaxArchiveEntries}），疑似解压炸弹。"));
                return result;
            }

            long accumulatedBytes = 0;
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                accumulatedBytes += Math.Max(0, entry.Length);
                if (accumulatedBytes > MaxExpandedBytes)
                {
                    result.Violations.Add(new SandboxViolation(
                        entry.FullName,
                        "ZipBomb.TotalBytesExceeded",
                        $"累计解压体积超过 1GB 安全上限，导入已强制中断。"));
                    break;
                }

                // 路径穿越校验
                if (HasPathTraversal(entry.FullName))
                {
                    result.Violations.Add(new SandboxViolation(
                        entry.FullName,
                        "PathTraversal",
                        "包含相对路径跳脱符（..）或绝对盘符，疑似路径逃逸攻击。"));
                }

                // 危险脚本扩展名校验
                var ext = Path.GetExtension(entry.Name);
                if (BlockedScriptExtensions.Contains(ext))
                {
                    result.Violations.Add(new SandboxViolation(
                        entry.FullName,
                        "DangerousExtension",
                        $"包含危险的系统脚本或可执行载荷（{ext}），禁止导入。"));
                }
            }

            result.TotalBytes = accumulatedBytes;
        }
        catch (InvalidDataException)
        {
            result.Violations.Add(new SandboxViolation(packagePath, "CorruptedArchive", "文件不是合法的 Zip 压缩格式或已损坏。"));
        }
        catch (Exception ex)
        {
            result.Violations.Add(new SandboxViolation(packagePath, "InspectionError", $"沙箱解构异常：{ex.Message}"));
        }

        return result;
    }

    public SandboxInspectionResult InspectThemePackage(string packagePath)
    {
        var result = new SandboxInspectionResult
        {
            PackagePath = packagePath,
            PackageType = "ThemePackage (.ummtheme)"
        };

        if (!File.Exists(packagePath))
        {
            result.Violations.Add(new SandboxViolation(packagePath, "FileNotFound", "目标主题包文件在磁盘上不存在或已被移除。"));
            return result;
        }

        var fileInfo = new FileInfo(packagePath);
        if (fileInfo.Length > MaxThemePackageSizeBytes)
        {
            result.Violations.Add(new SandboxViolation(
                packagePath,
                "ThemeSizeExceeded",
                $"主题包体积（{fileInfo.Length / (1024 * 1024)}MB）超过 50MB 安全上限。"));
            return result;
        }

        try
        {
            using var zip = ZipFile.OpenRead(packagePath);
            result.EntryCount = zip.Entries.Count;

            var hasManifest = false;
            long accumulatedBytes = 0;

            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                accumulatedBytes += Math.Max(0, entry.Length);

                if (HasPathTraversal(entry.FullName))
                {
                    result.Violations.Add(new SandboxViolation(
                        entry.FullName,
                        "PathTraversal",
                        "主题包条目包含相对路径跳脱符（..）或嵌套目录逃逸。"));
                }

                var normalized = entry.FullName.Replace('\\', '/').TrimStart('/');
                if (normalized.Equals("theme.json", StringComparison.OrdinalIgnoreCase))
                {
                    hasManifest = true;
                    continue;
                }

                var ext = Path.GetExtension(entry.Name);
                if (ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) || BlockedScriptExtensions.Contains(ext))
                {
                    result.Violations.Add(new SandboxViolation(
                        entry.FullName,
                        "DangerousPayloadInTheme",
                        $"主题包中严禁包含任何二进制代码或可执行脚本（发现 {ext}）。"));
                }
                else if (!AllowedThemeExtensions.Contains(ext))
                {
                    result.Violations.Add(new SandboxViolation(
                        entry.FullName,
                        "UnsupportedThemeAsset",
                        $"主题包仅允许包含 theme.json 与壁纸图片（.png, .jpg, .jpeg, .webp），不允许包含 {ext}。"));
                }
            }

            if (!hasManifest)
            {
                result.Violations.Add(new SandboxViolation(
                    packagePath,
                    "MissingManifest",
                    "主题包缺失必需的 theme.json 清单。"));
            }

            result.TotalBytes = accumulatedBytes;
        }
        catch (InvalidDataException)
        {
            result.Violations.Add(new SandboxViolation(packagePath, "CorruptedArchive", "文件不是合法的 Zip 压缩格式或已损坏。"));
        }
        catch (Exception ex)
        {
            result.Violations.Add(new SandboxViolation(packagePath, "InspectionError", $"沙箱解构异常：{ex.Message}"));
        }

        return result;
    }

    private static bool HasPathTraversal(string entryFullName)
    {
        if (string.IsNullOrWhiteSpace(entryFullName)) return false;
        var normalized = entryFullName.Replace('\\', '/');
        return normalized.Contains("../")
            || normalized.Contains("/..")
            || normalized.Equals("..", StringComparison.Ordinal)
            || normalized.Contains(':')
            || normalized.StartsWith('/')
            || normalized.Contains('\0');
    }
}
