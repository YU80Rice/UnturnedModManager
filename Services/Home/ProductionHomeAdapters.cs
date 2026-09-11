using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnturnedModManager.Domain.Home;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services.Home;

/// <summary>
/// 生产环境启动状态适配器
/// </summary>
public sealed class ProductionLaunchStateAdapter : ILaunchStateAdapter
{
    private readonly GamePathService _gamePaths;
    private readonly GameLaunchService _launcher;
    private readonly PluginProfileService _profiles;
    private readonly LocalModService _localMods;
    private readonly BepInExService _bepInEx;

    public ProductionLaunchStateAdapter(
        GamePathService gamePaths,
        GameLaunchService launcher,
        PluginProfileService profiles,
        LocalModService localMods,
        BepInExService bepInEx)
    {
        _gamePaths = gamePaths ?? throw new ArgumentNullException(nameof(gamePaths));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _localMods = localMods ?? throw new ArgumentNullException(nameof(localMods));
        _bepInEx = bepInEx ?? throw new ArgumentNullException(nameof(bepInEx));
    }

    public ValueTask<LaunchSectionSnapshot> GetLaunchSectionAsync(CancellationToken token = default)
    {
        string gamePath = AppSettings.UnturnedInstallPath ?? "";
        bool isPathValid = _gamePaths.IsValid(gamePath);
        bool canLaunch = isPathValid && !_launcher.IsRunning();

        var allProfiles = _profiles.GetProfiles();
        var activeId = _profiles.GetActiveProfileId();
        var activeProfile = allProfiles.FirstOrDefault(p => p.Id == activeId)
            ?? allProfiles.FirstOrDefault()
            ?? new PluginProfile { Name = "默认方案" };

        int activeMods = _localMods.Scan().Count(p => p.IsEnabled);
        bool isBepInstalled = _bepInEx.GetStatus(gamePath).IsInstalled;
        string modeTitle = "单人 / 本地模组";
        string modeSubtitle = isBepInstalled
            ? "BepInEx 插件环境就绪"
            : "纯净原版环境";

        var snapshot = new LaunchSectionSnapshot(
            ModeTitle: modeTitle,
            ModeSubtitle: modeSubtitle,
            GamePath: gamePath,
            CanLaunch: canLaunch,
            ActiveProfileName: activeProfile.Name,
            ActiveModCount: activeMods,
            IsConfigReady: isPathValid
        );

        return ValueTask.FromResult(snapshot);
    }
}

/// <summary>
/// 生产环境运行环境适配器
/// </summary>
public sealed class ProductionEnvironmentStateAdapter : IEnvironmentStateAdapter
{
    private readonly BepInExService _bepInEx;
    private readonly DxvkService _dxvk;
    private readonly DiagnosticService _diagnostics;

    public ProductionEnvironmentStateAdapter(
        BepInExService bepInEx,
        DxvkService dxvk,
        DiagnosticService diagnostics)
    {
        _bepInEx = bepInEx ?? throw new ArgumentNullException(nameof(bepInEx));
        _dxvk = dxvk ?? throw new ArgumentNullException(nameof(dxvk));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public ValueTask<EnvironmentSectionSnapshot> GetEnvironmentSectionAsync(CancellationToken token = default)
    {
        string gamePath = AppSettings.UnturnedInstallPath ?? "";
        bool isBepInstalled = _bepInEx.GetStatus(gamePath).IsInstalled;
        string loaderVersion = isBepInstalled ? BepInExService.SupportedVersion : "未安装";
        bool isDxvkEnabled = AppSettings.EnableDxvk;
        bool isFallback = !AppSettings.DxvkRecommendedByGpu.GetValueOrDefault(true);
        var diag = _diagnostics.Analyze(gamePath);
        bool isHealthy = diag.Severity != DiagnosticSeverity.Error;

        var snapshot = new EnvironmentSectionSnapshot(
            LoaderVersion: loaderVersion,
            IsLoaderEnabled: isBepInstalled,
            IsGraphicsAccelerationActive: isDxvkEnabled,
            IsFallbackRendering: isFallback,
            IsEnvironmentHealthy: isHealthy
        );

        return ValueTask.FromResult(snapshot);
    }
}

/// <summary>
/// 生产环境诊断状态适配器
/// </summary>
public sealed class ProductionDiagnosticStateAdapter : IDiagnosticStateAdapter
{
    private readonly DiagnosticService _diagnostics;

    public ProductionDiagnosticStateAdapter(DiagnosticService diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public ValueTask<DiagnosticSectionSnapshot> GetDiagnosticSectionAsync(CancellationToken token = default)
    {
        string gamePath = AppSettings.UnturnedInstallPath ?? "";
        var analysis = _diagnostics.Analyze(gamePath);
        int unresolved = analysis.Severity == DiagnosticSeverity.Information ? 0 : Math.Max(1, analysis.Evidence.Count);
        bool canExport = !string.IsNullOrWhiteSpace(gamePath);

        var snapshot = new DiagnosticSectionSnapshot(
            HealthSummary: analysis.Summary,
            UnresolvedIssuesCount: unresolved,
            CanExportDiagnostics: canExport
        );

        return ValueTask.FromResult(snapshot);
    }
}

/// <summary>
/// 生产环境社区推荐模组适配器 (对接缓存与 API，五态优雅降级)
/// </summary>
public sealed class ProductionCommunityContentAdapter : ICommunityContentAdapter
{
    private readonly CommunityApiClient _api;
    private readonly CommunityCacheService _cache;
    private const string CacheKey = "featured_plugins";

    public ProductionCommunityContentAdapter(CommunityApiClient api, CommunityCacheService cache)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<FeedSectionSnapshot<FeaturedPluginItem>> GetFeaturedFeedAsync(CancellationToken token = default)
    {
        try
        {
            var mods = await _api.GetModsAsync(category: null, search: null, sort: "popular", token: token);
            var items = mods.Take(6).Select(p => new FeaturedPluginItem(
                PluginId: p.Id.ToString(),
                DisplayName: p.DisplayTitle,
                ShortDescription: p.DisplayDescription,
                CategoryTag: p.CategoryDisplay,
                Version: p.EffectiveVersion
            )).ToImmutableArray();

            var now = DateTimeOffset.UtcNow;
            if (items.IsEmpty)
            {
                return FeedSectionSnapshot<FeaturedPluginItem>.Empty(now);
            }

            // 写入本地缓存以备离线
            await _cache.WriteAsync(CacheKey, items.ToList(), token);
            return FeedSectionSnapshot<FeaturedPluginItem>.LiveOnline(items, now);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw; // 协调器取消向上传播
        }
        catch (Exception ex)
        {
            var category = ClassifyException(ex);

            // 网络或解析失败时，尝试读取本地缓存进行降级
            var cached = await _cache.ReadStaleAsync<List<FeaturedPluginItem>>(CacheKey, token);
            if (cached is { Count: > 0 })
            {
                var cachedArray = cached.ToImmutableArray();
                return FeedSectionSnapshot<FeaturedPluginItem>.CachedStale(
                    cachedArray,
                    DateTimeOffset.UtcNow.AddMinutes(-30));
            }

            return FeedSectionSnapshot<FeaturedPluginItem>.Failed(category, canRetry: true);
        }
    }

    private static FeedFailureCategory ClassifyException(Exception ex) => ex switch
    {
        TaskCanceledException or TimeoutException => FeedFailureCategory.Timeout,
        HttpRequestException => FeedFailureCategory.NetworkUnavailable,
        JsonException => FeedFailureCategory.DataCorrupted,
        _ => FeedFailureCategory.ServerUnavailable
    };
}

/// <summary>
/// 生产环境更新公告适配器 (对接 LauncherUpdateService 与 GitHub Release，五态降级)
/// </summary>
public sealed class ProductionReleaseAnnouncementAdapter : IReleaseAnnouncementAdapter
{
    private readonly LauncherUpdateService _launcherUpdates;

    public ProductionReleaseAnnouncementAdapter(LauncherUpdateService launcherUpdates)
    {
        _launcherUpdates = launcherUpdates ?? throw new ArgumentNullException(nameof(launcherUpdates));
    }

    public async Task<FeedSectionSnapshot<PatchNoteItem>> GetAnnouncementsFeedAsync(CancellationToken token = default)
    {
        try
        {
            var currentVersion = typeof(ProductionReleaseAnnouncementAdapter).Assembly.GetName().Version ?? new Version(1, 0, 0);
            var checkResult = await _launcherUpdates.CheckLatestReleaseAsync(currentVersion, token);

            if (checkResult.LatestRelease is { } notes)
            {
                var highlights = ParseHighlights(notes.ReleaseNotes);
                var item = new PatchNoteItem(
                    VersionTag: notes.Version.ToString(3),
                    ReleasedAt: notes.PublishedAt ?? DateTimeOffset.UtcNow,
                    Highlights: highlights
                );

                var items = ImmutableArray.Create(item);
                return FeedSectionSnapshot<PatchNoteItem>.LiveOnline(items, DateTimeOffset.UtcNow);
            }

            return FeedSectionSnapshot<PatchNoteItem>.Empty(DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var category = ex switch
            {
                TaskCanceledException or TimeoutException => FeedFailureCategory.Timeout,
                HttpRequestException => FeedFailureCategory.NetworkUnavailable,
                JsonException => FeedFailureCategory.DataCorrupted,
                _ => FeedFailureCategory.ServerUnavailable
            };

            return FeedSectionSnapshot<PatchNoteItem>.Failed(category, canRetry: true);
        }
    }

    private static ImmutableArray<string> ParseHighlights(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return ImmutableArray<string>.Empty;

        var lines = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && (line.StartsWith("-") || line.StartsWith("*")))
            .Select(line => line.TrimStart('-', '*', ' '))
            .Take(5)
            .ToImmutableArray();

        return lines.IsEmpty ? ImmutableArray.Create(notes.Trim()) : lines;
    }
}

/// <summary>
/// 生产环境吉祥物展示与台词适配器
/// </summary>
public sealed class ProductionMascotPresentationAdapter : IMascotPresentationAdapter
{
    private int _quoteIndex;
    private static readonly string[] Quotes =
    [
        "🐾 幸存者，今天也是充满模组的一天！",
        "🛠️ BepInEx 与 12 个本地模组正在为您保驾护航！",
        "🎮 DXVK Vulkan 帧率增强模式就绪，丝滑畅玩！",
        "☕ 探险累了记得常回主页看看，安全第一~",
        "🦊 嗷呜！今天也要元气满满地在 Unturned 里生存下去！"
    ];

    public MascotSectionSnapshot GetMascotSection()
    {
        return new MascotSectionSnapshot(
            IsVisible: AppSettings.IsHomeWelcomeEnabled,
            QuoteIndex: _quoteIndex,
            CurrentQuote: Quotes[Math.Abs(_quoteIndex) % Quotes.Length]
        );
    }

    public MascotSectionSnapshot NextQuote()
    {
        _quoteIndex = (_quoteIndex + 1) % Quotes.Length;
        return GetMascotSection();
    }
}
