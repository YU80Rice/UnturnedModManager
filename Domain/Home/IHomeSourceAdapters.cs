using System.Threading;
using System.Threading.Tasks;

namespace UnturnedModManager.Domain.Home;

/// <summary>
/// 游戏启动状态源适配器
/// </summary>
public interface ILaunchStateAdapter
{
    ValueTask<LaunchSectionSnapshot> GetLaunchSectionAsync(CancellationToken token = default);
}

/// <summary>
/// 运行环境与加载器状态源适配器
/// </summary>
public interface IEnvironmentStateAdapter
{
    ValueTask<EnvironmentSectionSnapshot> GetEnvironmentSectionAsync(CancellationToken token = default);
}

/// <summary>
/// 诊断与日志健康状态源适配器
/// </summary>
public interface IDiagnosticStateAdapter
{
    ValueTask<DiagnosticSectionSnapshot> GetDiagnosticSectionAsync(CancellationToken token = default);
}

/// <summary>
/// 社区推荐模组分区源适配器 (慢速网络/缓存，五态离线降级)
/// </summary>
public interface ICommunityContentAdapter
{
    Task<FeedSectionSnapshot<FeaturedPluginItem>> GetFeaturedFeedAsync(CancellationToken token = default);
}

/// <summary>
/// 版本公告与更新日志源适配器 (慢速网络/缓存，五态离线降级)
/// </summary>
public interface IReleaseAnnouncementAdapter
{
    Task<FeedSectionSnapshot<PatchNoteItem>> GetAnnouncementsFeedAsync(CancellationToken token = default);
}

/// <summary>
/// 吉祥物展示与台词适配器
/// </summary>
public interface IMascotPresentationAdapter
{
    MascotSectionSnapshot GetMascotSection();
    MascotSectionSnapshot NextQuote();
}
