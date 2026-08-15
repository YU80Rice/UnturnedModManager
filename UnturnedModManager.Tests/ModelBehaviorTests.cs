using System.Text.Json;
using System.Net;
using System.Net.Http;
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
    public void CommunityDependency_ExposesLocalizedDisplayTitle()
    {
        var dependency = JsonSerializer.Deserialize<CommunityDependency>(
            "{\"id\":7,\"title\":{\"zh\":\"核心前置\",\"en\":\"Core dependency\"},\"version\":\"1.0.0\"}");

        Assert.NotNull(dependency);
        Assert.Equal("核心前置", dependency!.DisplayTitle);
        Assert.Equal("1.0.0", dependency.Version);
    }

    [Fact]
    public void SingleInstanceService_RejectsSecondOwner()
    {
        var mutexName = $"Local\\UnturnedModManager.Tests.{Guid.NewGuid():N}";
        using var first = new SingleInstanceService(mutexName);
        using var second = new SingleInstanceService(mutexName);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public async Task SingleInstanceService_ForwardsSecondaryActivationToPrimary()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var mutexName = $"Local\\UnturnedModManager.Tests.{suffix}";
        var pipeName = $"UnturnedModManager.Tests.{suffix}";
        using var primary = new SingleInstanceService(mutexName, pipeName);
        var activated = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.Activated += args => activated.TrySetResult(args);

        Assert.True(primary.TryAcquire([]));
        primary.StartListening();

        using var secondary = new SingleInstanceService(mutexName, pipeName);
        Assert.False(secondary.TryAcquire(["umm://install/42"]));

        var args = await activated.Task.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.Equal(["umm://install/42"], args);
    }

    [Fact]
    public void ProtocolRegistrar_ParsesOnlySafePositiveInstallIntents()
    {
        Assert.True(ProtocolRegistrar.TryParseInstallIntent("umm://install/42?source=web", out var uriId));
        Assert.Equal(42, uriId);
        Assert.True(ProtocolRegistrar.TryParseInstallIntent("UMM:install/7", out var compactId));
        Assert.Equal(7, compactId);
        Assert.Equal(99, ProtocolRegistrar.FindInstallIntent(["--install", "99"]));

        Assert.False(ProtocolRegistrar.TryParseInstallIntent("unmod://install/42", out _));
        Assert.False(ProtocolRegistrar.TryParseInstallIntent("umm://remove/42", out _));
        Assert.False(ProtocolRegistrar.TryParseInstallIntent("umm://install/0", out _));
        Assert.False(ProtocolRegistrar.TryParseInstallIntent("umm://install/not-a-number", out _));
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

    [Fact]
    public async Task DownloadService_SkipsCommunitySourceWhenSignedOut()
    {
        var requested = new List<string>();
        var progress = new List<OperationProgress>();
        var destination = Path.Combine(Path.GetTempPath(), "umm-download-" + Guid.NewGuid().ToString("N"));

        try
        {
            var downloads = new HttpDownloadService(
                communityTokenProvider: () => null,
                clientFactory: _ => new HttpClient(new RecordingHandler(requested, "fallback")));

            await downloads.DownloadAsync(
                [
                    new("unmod.online 社区源", "https://unmod.online/api/mods/4/file", TimeSpan.FromSeconds(1), RequiresCommunityAuth: true),
                    new("国内镜像", "https://example.invalid/fallback.zip", TimeSpan.FromSeconds(1))
                ],
                destination,
                new Progress<OperationProgress>(progress.Add));

            Assert.Equal(["https://example.invalid/fallback.zip"], requested);
            Assert.Contains(progress, item => item.Message.Contains("需要登录 unmod.online", StringComparison.Ordinal));
            Assert.Equal("fallback", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            if (File.Exists(destination)) File.Delete(destination);
        }
    }

    [Fact]
    public async Task DownloadService_SendsCommunityTokenOnlyAsCookie()
    {
        var handler = new RecordingHandler([], "community");
        var destination = Path.Combine(Path.GetTempPath(), "umm-download-" + Guid.NewGuid().ToString("N"));

        try
        {
            var downloads = new HttpDownloadService(
                communityTokenProvider: () => "test-token",
                clientFactory: _ => new HttpClient(handler));

            await downloads.DownloadAsync(
                [new("unmod.online 社区源", "https://unmod.online/api/mods/4/file", TimeSpan.FromSeconds(1), RequiresCommunityAuth: true)],
                destination);

            Assert.NotNull(handler.LastRequest);
            Assert.Equal("token=test-token", handler.LastRequest!.Headers.GetValues("Cookie").Single());
            Assert.Equal("community", await File.ReadAllTextAsync(destination));
        }
        finally
        {
            if (File.Exists(destination)) File.Delete(destination);
        }
    }

    private sealed class RecordingHandler(ICollection<string> requestedUrls, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            requestedUrls.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(responseBody)
            });
        }
    }
}
