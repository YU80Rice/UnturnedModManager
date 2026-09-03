using System.IO.Compression;
using System.Text;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class SandboxInspectionTests
{
    private static string CreateZipArchive(Action<ZipArchive> populate)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sandbox_test_{Guid.NewGuid():N}.zip");
        using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Create))
        {
            populate(zip);
        }
        return tempFile;
    }

    [Fact]
    public void InspectModPackage_WithDangerousScript_ReturnsViolation()
    {
        var zipPath = CreateZipArchive(zip =>
        {
            var entry = zip.CreateEntry("BepInEx/plugins/hack.bat");
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes("@echo off\r\ncalc.exe"));
        });

        try
        {
            var service = new PackageSandboxInspectionService();
            var result = service.InspectModPackage(zipPath);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.EntryPath.Contains("hack.bat"));
            Assert.Contains(result.Violations, v => v.RuleName.Contains("DangerousExtension") || v.RuleName.Contains("DangerousPayload"));
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void InspectModPackage_WithPathTraversal_ReturnsViolation()
    {
        var zipPath = CreateZipArchive(zip =>
        {
            var entry = zip.CreateEntry("../evil.dll");
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes("fake dll"));
        });

        try
        {
            var service = new PackageSandboxInspectionService();
            var result = service.InspectModPackage(zipPath);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.EntryPath.Contains("..") || v.RuleName.Contains("PathTraversal"));
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void InspectThemePackage_WithForbiddenDll_ReturnsViolation()
    {
        var zipPath = CreateZipArchive(zip =>
        {
            var themeEntry = zip.CreateEntry("theme.json");
            using (var stream = themeEntry.Open())
            {
                stream.Write(Encoding.UTF8.GetBytes("{\"Name\":\"Dark\"}"));
            }
            var dllEntry = zip.CreateEntry("stealth.dll");
            using (var stream = dllEntry.Open())
            {
                stream.Write(Encoding.UTF8.GetBytes("fake dll in theme"));
            }
        });

        try
        {
            var service = new PackageSandboxInspectionService();
            var result = service.InspectThemePackage(zipPath);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.EntryPath.Contains("stealth.dll"));
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void InspectThemePackage_WithValidThemeAssets_Passes()
    {
        var zipPath = CreateZipArchive(zip =>
        {
            var themeEntry = zip.CreateEntry("theme.json");
            using (var stream = themeEntry.Open())
            {
                stream.Write(Encoding.UTF8.GetBytes("{\"Name\":\"CleanTheme\"}"));
            }
            var bgEntry = zip.CreateEntry("wallpaper.png");
            using (var stream = bgEntry.Open())
            {
                stream.Write(Encoding.UTF8.GetBytes("fake png content"));
            }
        });

        try
        {
            var service = new PackageSandboxInspectionService();
            var result = service.InspectThemePackage(zipPath);

            Assert.True(result.IsValid);
            Assert.Empty(result.Violations);
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    [Fact]
    public void FormatDiagnosticReport_ContainsPackagePathAndViolations()
    {
        var result = new SandboxInspectionResult
        {
            PackagePath = "D:\\bad_package.ummpk",
            PackageType = "ModPackage"
        };
        result.Violations.Add(new SandboxViolation("setup.exe", "DangerousExtension", "包含禁止的可执行文件"));

        var report = result.FormatDiagnosticReport();
        Assert.Contains("D:\\bad_package.ummpk", report);
        Assert.Contains("setup.exe", report);
        Assert.Contains("DangerousExtension", report);
    }
}
