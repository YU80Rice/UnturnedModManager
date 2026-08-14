using System.Text.Json;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class ModelBehaviorTests
{
    [Fact]
    public void LocalizedTextConverter_PrefersRequestedLanguageThenFallsBack()
    {
        const string json = "{\"zh\":\"中文标题\",\"en\":\"English title\"}";
        var value = JsonSerializer.Deserialize<LocalizedText>(json);

        Assert.NotNull(value);
        Assert.Equal("中文标题", value!.Pick("zh"));
        Assert.Equal("English title", value.Pick("en"));
        Assert.Equal("中文标题", value.Pick("ja"));

        var roundTrip = JsonSerializer.Serialize(value);
        var restored = JsonSerializer.Deserialize<LocalizedText>(roundTrip);
        Assert.NotNull(restored);
        Assert.Equal("中文标题", restored!.Pick("zh"));
    }

    [Fact]
    public void CommunityMod_UsesLocalizedTitleAndVersionMetadata()
    {
        const string json = "{\"id\":42,\"title\":{\"zh\":\"测试插件\",\"en\":\"Test Mod\"},\"version\":\"1.2.3\",\"author_name\":\"tester\",\"downloads\":7}";
        var mod = JsonSerializer.Deserialize<CommunityMod>(json);

        Assert.NotNull(mod);
        Assert.Equal(42, mod!.Id);
        Assert.Equal("测试插件", mod.DisplayTitle);
        Assert.Contains("v1.2.3", mod.Meta);
        Assert.Contains("7", mod.Meta);
    }

    [Fact]
    public void ModItem_DetectsUpdatesWithoutTreatingVersionPrefixAsChange()
    {
        var item = new ModItem
        {
            AssemblyName = "ExampleMod",
            InstalledVersion = "v1.0.0",
            IsCommunityManaged = true,
            CommunityModId = 42,
            RemoteVersion = "1.0.0"
        };

        Assert.Equal("ExampleMod", item.DisplayTitle);
        Assert.True(item.IsCommunityMatched);
        Assert.False(item.HasUpdate);

        item.RemoteVersion = "1.1.0";

        Assert.True(item.HasUpdate);
    }

    [Fact]
    public async Task CommunityCache_PreservesFreshAndStaleMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), "umm-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new CommunityCacheService(directory);
            await cache.WriteAsync("mods|test", new[] { "one", "two" });

            var fresh = await cache.ReadAsync<string[]>("mods|test", TimeSpan.FromMinutes(1));
            var expired = await cache.ReadAsync<string[]>("mods|test", TimeSpan.Zero);
            var stale = await cache.ReadStaleAsync<string[]>("mods|test");

            Assert.Equal(new[] { "one", "two" }, fresh);
            Assert.Null(expired);
            Assert.Equal(new[] { "one", "two" }, stale);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BepInExUninstall_RemovesLoaderCoreButPreservesPlayerData()
    {
        var gameRoot = Path.Combine(Path.GetTempPath(), "umm-bepinex-uninstall-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(gameRoot, "BepInEx", "core"));
            Directory.CreateDirectory(Path.Combine(gameRoot, "BepInEx", "plugins"));
            Directory.CreateDirectory(Path.Combine(gameRoot, "BepInEx", "config"));
            Directory.CreateDirectory(Path.Combine(gameRoot, "BepInEx", "cache"));
            File.WriteAllText(Path.Combine(gameRoot, "Unturned.exe"), "test");
            File.WriteAllText(Path.Combine(gameRoot, "winhttp.dll"), "loader");
            File.WriteAllText(Path.Combine(gameRoot, "doorstop_config.ini"), "loader");
            File.WriteAllText(Path.Combine(gameRoot, ".doorstop_version"), "loader");
            File.WriteAllText(Path.Combine(gameRoot, "changelog.txt"), "player-owned changelog");
            File.WriteAllText(Path.Combine(gameRoot, "BepInEx", "core", "BepInEx.dll"), "core");
            File.WriteAllText(Path.Combine(gameRoot, "BepInEx", "plugins", "KeepMe.dll"), "plugin");
            File.WriteAllText(Path.Combine(gameRoot, "BepInEx", "config", "KeepMe.cfg"), "config");
            File.WriteAllText(Path.Combine(gameRoot, "BepInEx", "cache", "KeepMe.cache"), "cache");
            File.WriteAllText(Path.Combine(gameRoot, "BepInEx", "LogOutput.log"), "log");

            var service = new BepInExService(new HttpDownloadService());
            var result = service.Uninstall(gameRoot);

            Assert.True(result.Success);
            Assert.Equal("5.4.23.5", BepInExService.SupportedVersion);
            Assert.False(Directory.Exists(Path.Combine(gameRoot, "BepInEx", "core")));
            Assert.False(File.Exists(Path.Combine(gameRoot, "winhttp.dll")));
            Assert.False(File.Exists(Path.Combine(gameRoot, "doorstop_config.ini")));
            Assert.True(File.Exists(Path.Combine(gameRoot, "changelog.txt")));
            Assert.True(File.Exists(Path.Combine(gameRoot, "BepInEx", "plugins", "KeepMe.dll")));
            Assert.True(File.Exists(Path.Combine(gameRoot, "BepInEx", "config", "KeepMe.cfg")));
            Assert.True(File.Exists(Path.Combine(gameRoot, "BepInEx", "cache", "KeepMe.cache")));
            Assert.True(File.Exists(Path.Combine(gameRoot, "BepInEx", "LogOutput.log")));
        }
        finally
        {
            if (Directory.Exists(gameRoot)) Directory.Delete(gameRoot, true);
        }
    }
}
