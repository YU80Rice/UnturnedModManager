namespace UnturnedModManager.Navigation;

/// <summary>
/// 五大核心主槽位枚举 (严格映射 Unturned 原版 HUD)
/// </summary>
public enum PrimarySlot
{
    Play,       // Slot 1: 游戏启动
    Data,       // Slot 2: 角色与存档 (只读结构预览占位)
    Mods,       // Slot 3: 模组工坊 (聚合入口)
    Settings,   // Slot 4: 启动器设置
    Exit        // Slot 5: 退出程序
}

/// <summary>
/// 顶栏次级导航目标
/// </summary>
public enum SecondaryNavigationTarget
{
    TaskCenter, // 任务中心
    About       // 关于 UMM
}

/// <summary>
/// 模组工坊双子域枚举
/// </summary>
public enum WorkshopSubDomain
{
    LocalInstalled,     // 本地插件管理
    CommunityDiscovery  // 社区在线发现
}

/// <summary>
/// 导航执行结果指示
/// </summary>
public enum NavigationResult
{
    Success,
    Rejected,
    InvalidTarget,
    PendingInitialization
}
