using System.Text.Json;
using System.Net;
using System.Net.Http;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnturnedModManager.Helpers;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using UnturnedModManager.ViewModels;
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

    [Theory]
    [InlineData("moderator", "社区管理员")]
    [InlineData("creator", "创作者")]
    [InlineData("admin|creator", "社区管理员 · 创作者")]
    [InlineData("", "社区成员")]
    public void CommunityUser_MapsOnlyServerRoleToLocalizedIdentity(string role, string expected)
    {
        var user = new CommunityUser { Username = "YU80Rice", Role = role };

        Assert.Equal(expected, user.RoleDisplay);
        Assert.Equal($"{expected} · YU80Rice", user.DisplayIdentity);
    }

    [Theory]
    [InlineData("v2.1.2", "2.1.2")]
    [InlineData("2.3", "2.3.0")]
    [InlineData("not-a-version", null)]
    public void LauncherUpdateService_ParsesOnlyValidReleaseVersions(string tag, string? expected)
    {
        var parsed = LauncherUpdateService.TryParseReleaseVersion(tag, out var version);

        Assert.Equal(expected is not null, parsed);
        if (expected is not null)
            Assert.Equal(expected, version.ToString(3));
    }

    [Theory]
    [InlineData("sha256:1b6f406dd6a350f26731417dbed53a089d2a70f0b0049825f5c1eebf14400297", "1B6F406DD6A350F26731417DBED53A089D2A70F0B0049825F5C1EEBF14400297")]
    [InlineData("sha256:invalid", null)]
    [InlineData("", null)]
    public void LauncherUpdateService_AcceptsOnlyCompleteSha256Digests(string digest, string? expected) =>
        Assert.Equal(expected, LauncherUpdateService.NormalizeSha256Digest(digest));

    [Fact]
    public async Task LauncherUpdateService_DownloadsOnlyAValidatedOfficialReleaseAsset()
    {
        var directory = Path.Combine(Path.GetTempPath(), "umm-launcher-update-" + Guid.NewGuid().ToString("N"));
        var content = Encoding.UTF8.GetBytes("validated-launcher");
        var digest = Convert.ToHexString(SHA256.HashData(content));
        const string assetName = "UnturnedModManager-v2.1.2-win-x64.exe";
        var assetUrl = "https://github.com/YU80Rice/UnturnedModManager/releases/download/v2.1.2/" + assetName;
        var releaseJson = $$"""
        {"tag_name":"v2.1.2","name":"UMM v2.1.2","assets":[{"name":"{{assetName}}","browser_download_url":"{{assetUrl}}","size":{{content.Length}},"digest":"sha256:{{digest}}"}]}
        """;

        try
        {
            using var client = new HttpClient(new ReleaseHandler(releaseJson, assetUrl, content));
            using var updates = new LauncherUpdateService(client, directory);
            var update = await updates.CheckForUpdateAsync(new Version(2, 1, 1, 0));

            Assert.NotNull(update);
            Assert.Equal("v2.1.2", update!.DisplayVersion);
            var downloaded = await updates.DownloadAsync(update);
            Assert.Equal(content, await File.ReadAllBytesAsync(downloaded));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task LauncherUpdateService_ReturnsLatestReleaseNotesWhenLocalBuildIsNewer()
    {
        const string releaseJson = """
        {"tag_name":"v2.1.5","name":"UMM v2.1.5","body":"## 更新\n\n- 第一项\n- 第二项","assets":[]}
        """;

        using var client = new HttpClient(new ReleaseHandler(releaseJson, "", []));
        using var updates = new LauncherUpdateService(client, null);

        var result = await updates.CheckLatestReleaseAsync(new Version(2, 1, 6, 0));

        Assert.Null(result.AvailableUpdate);
        Assert.NotNull(result.LatestRelease);
        Assert.Equal("v2.1.5", result.LatestRelease!.DisplayVersion);
        Assert.Contains("第一项", result.LatestRelease.ReleaseNotes);
    }

    [Fact]
    public void HomeAnnouncement_ExtractsReleaseBulletHighlights()
    {
        var fallback = new[] { "兜底摘要" };
        var highlights = HomeViewModel.ExtractAnnouncementHighlights(
            "## 标题\n\n- **第一项**\n* `第二项`\n\n普通段落",
            fallback);

        Assert.Equal(["第一项", "第二项"], highlights);
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
    public void GpuDetector_PrefersActivePhysicalDgpuOverVirtualAndIntegratedAdapters()
    {
        var virtualAdapter = GpuDetector.Classify("OrayIddDriver Device");
        virtualAdapter.IsVirtualAdapter = true;
        var integrated = GpuDetector.Classify("AMD Radeon(TM) Graphics");
        integrated.IsIntegratedAdapter = true;
        var discrete = GpuDetector.Classify("AMD Radeon RX 6850M XT");
        discrete.IsActiveAdapter = true;

        var primary = GpuDetector.SelectPrimary([virtualAdapter, integrated, discrete]);

        Assert.Equal("AMD Radeon RX 6850M XT", primary.Name);
        Assert.Equal(GpuVendor.Amd, primary.Vendor);
        Assert.Equal(GpuArchitecture.Rdna2, primary.Architecture);
        Assert.Equal(DxvkRecommendation.Recommended, primary.DxvkRecommendation);
    }

    [Theory]
    [InlineData("Fluent", ThemePalette.Fluent)]
    [InlineData("warmPaper", ThemePalette.WarmPaper)]
    [InlineData("MistyForest", ThemePalette.MistyForest)]
    [InlineData("OceanDusk", ThemePalette.OceanDusk)]
    [InlineData("Lavender", ThemePalette.Lavender)]
    [InlineData("KleinBlue", ThemePalette.KleinBlue)]
    public void ThemeService_ParsesEveryPublishedPalette(string value, ThemePalette expected) =>
        Assert.Equal(expected, ThemeService.ParsePalette(value));

    [Fact]
    public void PluginProfiles_SaveAndAtomicallyRestorePluginEnablement()
    {
        var root = Path.Combine(Path.GetTempPath(), "umm-plugin-profile-" + Guid.NewGuid().ToString("N"));
        try
        {
            var gameRoot = Path.Combine(root, "Unturned");
            var plugins = Path.Combine(gameRoot, "BepInEx", "plugins");
            Directory.CreateDirectory(plugins);
            File.WriteAllText(Path.Combine(gameRoot, "Unturned.exe"), "test");
            File.WriteAllText(Path.Combine(plugins, "Alpha.dll"), "alpha");
            File.WriteAllText(Path.Combine(plugins, "Beta.dll.disabled"), "beta");

            var installer = new CommunityModInstaller(Path.Combine(root, "community-state"));
            var localMods = new LocalModService(installer, () => gameRoot);
            var profiles = new PluginProfileService(localMods, () => gameRoot, Path.Combine(root, "profile-state"));

            var created = profiles.CreateFromCurrent("联机优化", localMods.Scan());

            Assert.True(created.Success);
            Assert.NotNull(created.Profile);
            Assert.Equal(2, created.Profile!.Plugins.Count);
            Assert.Contains(created.Profile.Plugins, item => item.RelativePath == "Alpha.dll" && item.Enabled);
            Assert.Contains(created.Profile.Plugins, item => item.RelativePath == "Beta.dll" && !item.Enabled);

            var changed = localMods.Scan();
            Assert.True(localMods.SetEnabled(changed.Single(item => item.FileName == "Alpha.dll"), false).Success);
            Assert.True(localMods.SetEnabled(changed.Single(item => item.FileName == "Beta.dll"), true).Success);

            var applied = profiles.Apply(created.Profile.Id);

            Assert.True(applied.Success);
            Assert.True(File.Exists(Path.Combine(plugins, "Alpha.dll")));
            Assert.True(File.Exists(Path.Combine(plugins, "Beta.dll.disabled")));
            Assert.Equal(created.Profile.Id, profiles.GetActiveProfileId());

            var deleted = profiles.Delete(created.Profile.Id);
            Assert.True(deleted.Success);
            Assert.Empty(profiles.GetProfiles());
            Assert.True(File.Exists(Path.Combine(plugins, "Alpha.dll")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DiagnosticService_ReportsFatalLogEvidence()
    {
        var gameRoot = Path.Combine(Path.GetTempPath(), "umm-diagnostic-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logs = Path.Combine(gameRoot, "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "Client_Prev.log"), "normal line\nFatal error: access violation in UnityPlayer\n");

            var analysis = new DiagnosticService(gameRoot).Analyze(gameRoot);

            Assert.Equal(DiagnosticSeverity.Error, analysis.Severity);
            Assert.Contains("异常退出", analysis.Title);
            Assert.Contains(analysis.Evidence, item => item.Contains("Fatal error", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(gameRoot)) Directory.Delete(gameRoot, true);
        }
    }

    [Fact]
    public void DiagnosticService_ReportsDxvkInitializationEvidence()
    {
        var gameRoot = Path.Combine(Path.GetTempPath(), "umm-dxvk-diagnostic-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(gameRoot);
            File.WriteAllText(Path.Combine(gameRoot, "Unturned_d3d11.log"), "warn: dxvk: failed to create Vulkan device\n");

            var analysis = new DiagnosticService(gameRoot).Analyze(gameRoot);

            Assert.Equal(DiagnosticSeverity.Warning, analysis.Severity);
            Assert.Contains("DXVK", analysis.Title);
            Assert.Contains(analysis.Evidence, item => item.Contains("Unturned_d3d11.log"));
        }
        finally
        {
            if (Directory.Exists(gameRoot)) Directory.Delete(gameRoot, true);
        }
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

    [Fact]
    public async Task CommunityApi_DownloadsLatestGitHubReleaseForGitHubSource()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "umm-github-release-" + Guid.NewGuid().ToString("N"));
        var package = Encoding.UTF8.GetBytes("github-release-package");
        var digest = Convert.ToHexString(SHA256.HashData(package));
        const string assetUrl = "https://github.com/example/mod/releases/download/v1.2.3/mod.zip";
        var detailJson = """
        {"mod":{"id":42,"title":{"zh":"GitHub 测试插件"},"version":"1.0.0","github_repo":"example/mod","has_file":true}}
        """;
        var releaseJson = $$"""
        {"tag_name":"v1.2.3","assets":[{"name":"mod.zip","browser_download_url":"{{assetUrl}}","size":{{package.Length}},"digest":"sha256:{{digest}}"}]}
        """;

        try
        {
            var handler = new CommunityGitHubHandler(detailJson, releaseJson, package, assetUrl);
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unmod.online/") };
            using var api = new CommunityApiClient(new CommunityCacheService(cacheRoot), http);

            var detail = await api.GetModAsync(42);
            var downloaded = await api.DownloadAsync(detail, null);

            Assert.True(detail.IsGitHubReleaseResolved);
            Assert.Equal("1.0.0", detail.Version);
            Assert.Equal("v1.2.3", detail.EffectiveVersion);
            Assert.Equal(package.Length, detail.EffectiveFileSize);
            Assert.Equal("mod.zip", downloaded.FileName);
            Assert.Equal(package, downloaded.Content);
            Assert.Equal("GitHub 最新 Release", downloaded.Source);
            Assert.Equal("v1.2.3", downloaded.SourceVersion);
            Assert.Equal(0, handler.CommunityFallbackRequests);
            Assert.Equal(2, handler.GitHubReleaseRequests);
            Assert.Equal(1, handler.GitHubAssetRequests);
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        }
    }

    [Fact]
    public async Task CommunityApi_RejectsGitHubAssetWithInvalidDigestWithoutFallback()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "umm-github-integrity-" + Guid.NewGuid().ToString("N"));
        var package = Encoding.UTF8.GetBytes("tampered-package");
        const string assetUrl = "https://github.com/example/mod/releases/download/v1.2.3/mod.zip";
        const string wrongDigest = "3B5D5C3712955042212316173CCF37BE800E6A0AE2DCD7A3E2D2028B0A680B56";
        var releaseJson = $$"""
        {"tag_name":"v1.2.3","assets":[{"name":"mod.zip","browser_download_url":"{{assetUrl}}","size":{{package.Length}},"digest":"sha256:{{wrongDigest}}"}]}
        """;

        try
        {
            var handler = new CommunityGitHubHandler("{}", releaseJson, package, assetUrl);
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unmod.online/") };
            using var api = new CommunityApiClient(new CommunityCacheService(cacheRoot), http);
            var detail = new CommunityModDetail { Id = 42, GitHubRepository = "example/mod" };

            await Assert.ThrowsAnyAsync<IOException>(() => api.DownloadAsync(detail, null));

            Assert.Equal(0, handler.CommunityFallbackRequests);
            Assert.Equal(1, handler.GitHubReleaseRequests);
            Assert.Equal(1, handler.GitHubAssetRequests);
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        }
    }

    [Fact]
    public async Task CommunityApi_FallsBackToCommunityPackageWhenGitHubReleaseIsUnavailable()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "umm-github-fallback-" + Guid.NewGuid().ToString("N"));
        var fallbackPackage = Encoding.UTF8.GetBytes("community-package");

        try
        {
            var handler = new CommunityGitHubHandler("{}", "{}", fallbackPackage, "", HttpStatusCode.ServiceUnavailable);
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unmod.online/") };
            using var api = new CommunityApiClient(new CommunityCacheService(cacheRoot), http);
            var detail = new CommunityModDetail { Id = 42, GitHubRepository = "example/mod" };

            var downloaded = await api.DownloadAsync(detail, null);

            Assert.Equal(fallbackPackage, downloaded.Content);
            Assert.Equal("unmod.online 社区包", downloaded.Source);
            Assert.Equal(1, handler.GitHubReleaseRequests);
            Assert.Equal(1, handler.CommunityFallbackRequests);
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        }
    }

    [Fact]
    public async Task CommunityInstaller_RecordsCommunityVersionWhenGitHubAssetFallsBack()
    {
        var root = Path.Combine(Path.GetTempPath(), "umm-github-version-fallback-" + Guid.NewGuid().ToString("N"));
        var cacheRoot = Path.Combine(root, "cache");
        var stateRoot = Path.Combine(root, "state");
        var gameRoot = Path.Combine(root, "game");
        var package = CreateZipPackage("BepInEx/plugins/Example.dll", "community-v1");
        const string assetUrl = "https://github.com/example/mod/releases/download/v2.0.0/mod.zip";
        var digest = Convert.ToHexString(SHA256.HashData(package));
        var detailJson = """
        {"mod":{"id":42,"title":{"zh":"回退版本测试"},"version":"1.0.0","github_repo":"example/mod","has_file":true}}
        """;
        var releaseJson = $$"""
        {"tag_name":"v2.0.0","assets":[{"name":"mod.zip","browser_download_url":"{{assetUrl}}","size":{{package.Length}},"digest":"sha256:{{digest}}"}]}
        """;

        try
        {
            Directory.CreateDirectory(gameRoot);
            File.WriteAllText(Path.Combine(gameRoot, "Unturned.exe"), "test");
            var handler = new CommunityGitHubHandler(detailJson, releaseJson, package, assetUrl, assetStatus: HttpStatusCode.ServiceUnavailable);
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unmod.online/") };
            using var api = new CommunityApiClient(new CommunityCacheService(cacheRoot), http);
            var detail = await api.GetModAsync(42);
            var installer = new CommunityModInstaller(stateRoot);

            await installer.InstallWithDependenciesDetailedAsync(api, detail, gameRoot);

            var installed = Assert.Single(installer.GetInstalledMods());
            Assert.Equal("1.0.0", installed.Version);
            Assert.Equal("v2.0.0", detail.EffectiveVersion);
            Assert.NotEqual(installed.Version, detail.EffectiveVersion);
            Assert.Equal(1, handler.CommunityFallbackRequests);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LocalMods_SynchronizesEffectiveGitHubVersionForUpdateState()
    {
        var root = Path.Combine(Path.GetTempPath(), "umm-local-github-version-" + Guid.NewGuid().ToString("N"));
        var gameRoot = Path.Combine(root, "game");
        var stateRoot = Path.Combine(root, "state");
        var cacheRoot = Path.Combine(root, "cache");
        var pluginPath = Path.Combine(gameRoot, "BepInEx", "plugins", "Plugin.dll");
        const string assetUrl = "https://github.com/example/mod/releases/download/v2.0.0/mod.zip";
        const string detailJson = """
        {"mod":{"id":42,"title":{"zh":"GitHub 同步测试"},"version":"1.0.0","github_repo":"example/mod","has_file":true}}
        """;
        const string releaseJson = """
        {"tag_name":"v2.0.0","assets":[{"name":"mod.zip","browser_download_url":"https://github.com/example/mod/releases/download/v2.0.0/mod.zip","size":1}]}
        """;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pluginPath)!);
            File.WriteAllText(Path.Combine(gameRoot, "Unturned.exe"), "test");
            File.Copy(typeof(CommunityModInstaller).Assembly.Location, pluginPath);
            Directory.CreateDirectory(stateRoot);
            var manifest = new InstalledCommunityMod
            {
                RemoteId = 42,
                Title = "GitHub 同步测试",
                Version = "1.0.0",
                Files = [new InstalledCommunityFile { RelativePath = "BepInEx/plugins/Plugin.dll" }]
            };
            File.WriteAllText(Path.Combine(stateRoot, "42.json"), JsonSerializer.Serialize(manifest));

            var handler = new CommunityGitHubHandler(detailJson, releaseJson, [0], assetUrl);
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unmod.online/") };
            var installer = new CommunityModInstaller(stateRoot);
            var localMods = new LocalModService(installer, () => gameRoot);
            using var viewModel = new LocalModsViewModel(
                new CommunityApiClient(new CommunityCacheService(cacheRoot), http),
                installer,
                localMods,
                new UserDialogService());

            await viewModel.ActivateAsync();

            var item = Assert.Single(viewModel.Mods);
            Assert.Equal("v2.0.0", item.RemoteVersion);
            Assert.True(item.HasUpdate);

            item.InstalledVersion = "v2.0.0";
            Assert.False(item.HasUpdate);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CommunityApi_RejectsMalformedGitHubDigestWithoutDownloadingOrFallback()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "umm-github-malformed-digest-" + Guid.NewGuid().ToString("N"));
        var package = Encoding.UTF8.GetBytes("untrusted-package");
        const string assetUrl = "https://github.com/example/mod/releases/download/v1.2.3/mod.zip";
        const string malformedDigest = "sha256:not-a-valid-digest";
        var releaseJson = $$"""
        {"tag_name":"v1.2.3","assets":[{"name":"mod.zip","browser_download_url":"{{assetUrl}}","size":{{package.Length}},"digest":"{{malformedDigest}}"}]}
        """;

        try
        {
            var handler = new CommunityGitHubHandler("{}", releaseJson, package, assetUrl);
            using var http = new HttpClient(handler) { BaseAddress = new Uri("https://unmod.online/") };
            using var api = new CommunityApiClient(new CommunityCacheService(cacheRoot), http);
            var detail = new CommunityModDetail { Id = 42, GitHubRepository = "example/mod" };

            await Assert.ThrowsAnyAsync<IOException>(() => api.DownloadAsync(detail, null));

            Assert.Equal(1, handler.GitHubReleaseRequests);
            Assert.Equal(0, handler.GitHubAssetRequests);
            Assert.Equal(0, handler.CommunityFallbackRequests);
        }
        finally
        {
            if (Directory.Exists(cacheRoot)) Directory.Delete(cacheRoot, true);
        }
    }

    private static byte[] CreateZipPackage(string entryPath, string content)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write(content);
        }
        return memory.ToArray();
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

    private sealed class ReleaseHandler(string releaseJson, string assetUrl, byte[] assetContent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsoluteUri.EndsWith("/releases/latest", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(releaseJson, Encoding.UTF8, "application/json")
                });
            }

            Assert.Equal(assetUrl, request.RequestUri?.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(assetContent)
            });
        }
    }

    private sealed class CommunityGitHubHandler(
        string detailJson,
        string releaseJson,
        byte[] package,
        string assetUrl,
        HttpStatusCode latestReleaseStatus = HttpStatusCode.OK,
        HttpStatusCode assetStatus = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int GitHubReleaseRequests { get; private set; }
        public int GitHubAssetRequests { get; private set; }
        public int CommunityFallbackRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("请求 URL 缺失。");
            if (uri.AbsoluteUri.EndsWith("/api/mods/42", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(detailJson, Encoding.UTF8, "application/json")
                });
            }

            if (uri.AbsoluteUri.EndsWith("/releases/latest", StringComparison.Ordinal))
            {
                GitHubReleaseRequests++;
                return Task.FromResult(new HttpResponseMessage(latestReleaseStatus)
                {
                    Content = new StringContent(releaseJson, Encoding.UTF8, "application/json")
                });
            }

            if (!string.IsNullOrWhiteSpace(assetUrl) && uri.AbsoluteUri.Equals(assetUrl, StringComparison.Ordinal))
            {
                GitHubAssetRequests++;
                return Task.FromResult(new HttpResponseMessage(assetStatus)
                {
                    Content = new ByteArrayContent(package)
                });
            }

            if (uri.AbsoluteUri.EndsWith("/api/mods/42/file", StringComparison.Ordinal))
            {
                CommunityFallbackRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(package)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
