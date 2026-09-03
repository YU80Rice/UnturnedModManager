using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class ModPackageImportTests
{
    private static string CreateTestUmmpk(string name, Action<ZipArchive> populate)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_pkg_{Guid.NewGuid():N}.ummpk");
        using (var zip = ZipFile.Open(tempFile, ZipArchiveMode.Create))
        {
            var manifest = new UmmpkManifest
            {
                Name = name,
                Author = "Tester",
                Version = "1.2.0",
                Description = "A test package"
            };
            var manifestEntry = zip.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
            {
                writer.Write(JsonSerializer.Serialize(manifest));
            }
            populate(zip);
        }
        return tempFile;
    }

    [Fact]
    public void InspectPackagePlan_AccuratelyIdentifiesNewAndConflictingEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), "umm-test-inspect-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(root, "Game");
        var pluginsDir = Path.Combine(gameRoot, "BepInEx", "plugins");
        var configDir = Path.Combine(gameRoot, "BepInEx", "config");
        Directory.CreateDirectory(pluginsDir);
        Directory.CreateDirectory(configDir);

        File.WriteAllText(Path.Combine(pluginsDir, "ExistingMod.dll"), "local dll content");
        File.WriteAllText(Path.Combine(configDir, "LocalConfig.cfg"), "local key=val");

        var pkgPath = CreateTestUmmpk("TestConflictPackage", zip =>
        {
            var p1 = zip.CreateEntry("BepInEx/plugins/ExistingMod.dll");
            using (var s = p1.Open()) s.Write(Encoding.UTF8.GetBytes("new remote dll"));

            var p2 = zip.CreateEntry("BepInEx/plugins/BrandNewMod.dll");
            using (var s = p2.Open()) s.Write(Encoding.UTF8.GetBytes("new brand dll"));

            var c1 = zip.CreateEntry("BepInEx/config/LocalConfig.cfg");
            using (var s = c1.Open()) s.Write(Encoding.UTF8.GetBytes("remote key=val"));

            var c2 = zip.CreateEntry("BepInEx/config/BrandNewConfig.cfg");
            using (var s = c2.Open()) s.Write(Encoding.UTF8.GetBytes("new config"));
        });

        try
        {
            var localMods = new LocalModService(new CommunityModInstaller(Path.Combine(root, "state")), () => gameRoot);
            var profiles = new PluginProfileService(localMods, () => gameRoot, Path.Combine(root, "profiles"));

            var plan = profiles.InspectPackagePlan(pkgPath);

            Assert.Equal("TestConflictPackage", plan.Manifest.Name);
            Assert.Equal(4, plan.Entries.Count);
            Assert.Equal(1, plan.ConflictingDllCount);
            Assert.Equal(1, plan.ConflictingConfigCount);

            var existingModEntry = plan.Entries.Single(e => e.RelativePath.EndsWith("ExistingMod.dll"));
            Assert.Equal(ModPackageEntryConflictType.ConflictDll, existingModEntry.ConflictType);

            var newModEntry = plan.Entries.Single(e => e.RelativePath.EndsWith("BrandNewMod.dll"));
            Assert.Equal(ModPackageEntryConflictType.New, newModEntry.ConflictType);

            var localConfigEntry = plan.Entries.Single(e => e.RelativePath.EndsWith("LocalConfig.cfg"));
            Assert.Equal(ModPackageEntryConflictType.ConflictConfig, localConfigEntry.ConflictType);

            var newConfigEntry = plan.Entries.Single(e => e.RelativePath.EndsWith("BrandNewConfig.cfg"));
            Assert.Equal(ModPackageEntryConflictType.New, newConfigEntry.ConflictType);
        }
        finally
        {
            if (File.Exists(pkgPath)) File.Delete(pkgPath);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ImportPackageWithOptions_CreatesBakForConflictingDll_AndPreservesLocalConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), "umm-test-opt-import-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(root, "Game");
        var pluginsDir = Path.Combine(gameRoot, "BepInEx", "plugins");
        var configDir = Path.Combine(gameRoot, "BepInEx", "config");
        Directory.CreateDirectory(pluginsDir);
        Directory.CreateDirectory(configDir);

        var dllPath = Path.Combine(pluginsDir, "WeaponMod.dll");
        var cfgPath = Path.Combine(configDir, "WeaponMod.cfg");
        File.WriteAllText(dllPath, "v1.0.0 local dll");
        File.WriteAllText(cfgPath, "MyCustomKey=F");

        var pkgPath = CreateTestUmmpk("WeaponPack", zip =>
        {
            var p1 = zip.CreateEntry("BepInEx/plugins/WeaponMod.dll");
            using (var s = p1.Open()) s.Write(Encoding.UTF8.GetBytes("v2.0.0 remote dll"));

            var c1 = zip.CreateEntry("BepInEx/config/WeaponMod.cfg");
            using (var s = c1.Open()) s.Write(Encoding.UTF8.GetBytes("DefaultKey=E"));
        });

        try
        {
            var localMods = new LocalModService(new CommunityModInstaller(Path.Combine(root, "state")), () => gameRoot);
            var profiles = new PluginProfileService(localMods, () => gameRoot, Path.Combine(root, "profiles"));

            var options = new ModPackageImportOptions
            {
                CreateNewProfile = true,
                NewProfileName = "武器模组包独立方案",
                PreserveLocalConfig = true,
                BackupConflictingDlls = true
            };

            var result = profiles.ImportPackageWithOptions(pkgPath, options);
            Assert.True(result.Success);

            // Verify .bak backup of original dll
            var bakDll = Path.Combine(pluginsDir, "WeaponMod.dll.bak");
            Assert.True(File.Exists(bakDll));
            Assert.Equal("v1.0.0 local dll", File.ReadAllText(bakDll));

            // Verify new dll was extracted
            Assert.Equal("v2.0.0 remote dll", File.ReadAllText(dllPath));

            // Verify local config was preserved
            Assert.Equal("MyCustomKey=F", File.ReadAllText(cfgPath));
        }
        finally
        {
            if (File.Exists(pkgPath)) File.Delete(pkgPath);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
