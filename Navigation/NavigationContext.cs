namespace UnturnedModManager.Navigation;

/// <summary>
/// 强类型导航上下文抽象基类 (彻底杜绝弱类型字典与字符串路由)
/// </summary>
public abstract record NavigationContext;

/// <summary>
/// 插件详情上下文
/// 采用 string 表达唯一标识，业务数值标识转换由上层适配器负责。
/// </summary>
public sealed record PluginDetailContext(string ModId) : NavigationContext;

/// <summary>
/// 社区搜索上下文
/// </summary>
public sealed record CommunitySearchContext(string Query) : NavigationContext;
