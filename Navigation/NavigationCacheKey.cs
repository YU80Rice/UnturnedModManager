using System;

namespace UnturnedModManager.Navigation;

/// <summary>
/// 强类型不可变导航缓存索引键
/// 由不可变值对象构成，禁止包含 Page、ViewModel、委托引用或可变集合。
/// </summary>
public sealed record NavigationCacheKey(
    PrimarySlot Slot,
    WorkshopSubDomain? SubDomain = null,
    NavigationContext? Context = null)
{
    /// <summary>
    /// 校验目标槽位与子域、上下文组合的合法性不变量。
    /// </summary>
    public static bool IsValid(
        PrimarySlot slot,
        WorkshopSubDomain? subDomain,
        NavigationContext? context,
        out NavigationResult failureResult,
        out string? reason)
    {
        if (!Enum.IsDefined(typeof(PrimarySlot), slot))
        {
            failureResult = NavigationResult.InvalidTarget;
            reason = $"未定义的主槽位值: {slot}";
            return false;
        }

        if (subDomain.HasValue && !Enum.IsDefined(typeof(WorkshopSubDomain), subDomain.Value))
        {
            failureResult = NavigationResult.InvalidTarget;
            reason = $"未定义的工坊子域值: {subDomain.Value}";
            return false;
        }

        switch (slot)
        {
            case PrimarySlot.Play or PrimarySlot.Data or PrimarySlot.Settings or PrimarySlot.Exit:
                if (subDomain.HasValue)
                {
                    failureResult = NavigationResult.Rejected;
                    reason = $"{slot} 槽位不支持指定 WorkshopSubDomain。";
                    return false;
                }
                if (context is not null)
                {
                    failureResult = NavigationResult.Rejected;
                    reason = $"{slot} 槽位不支持附加 NavigationContext。";
                    return false;
                }
                break;

            case PrimarySlot.Mods:
                if (context is CommunitySearchContext)
                {
                    if (subDomain != WorkshopSubDomain.CommunityDiscovery)
                    {
                        failureResult = NavigationResult.Rejected;
                        reason = "社区搜索 (CommunitySearchContext) 必须显式指定为 CommunityDiscovery 子域。";
                        return false;
                    }
                }
                else if (context is PluginDetailContext)
                {
                    if (!subDomain.HasValue)
                    {
                        failureResult = NavigationResult.Rejected;
                        reason = "PluginDetailContext 请求必须显式指定所属子域 (LocalInstalled 或 CommunityDiscovery)，通用宿主不进行隐式猜测。";
                        return false;
                    }
                }
                else if (context is not null)
                {
                    failureResult = NavigationResult.Rejected;
                    reason = $"Mods 槽位不支持上下文类型: {context.GetType().Name}";
                    return false;
                }
                break;

            default:
                failureResult = NavigationResult.InvalidTarget;
                reason = $"未处理的槽位类型: {slot}";
                return false;
        }

        failureResult = NavigationResult.Success;
        reason = null;
        return true;
    }

    /// <summary>
    /// 校验当前缓存键实例自身组合是否合法。
    /// </summary>
    public bool IsValid(out NavigationResult failureResult, out string? reason)
        => IsValid(Slot, SubDomain, Context, out failureResult, out reason);
}
