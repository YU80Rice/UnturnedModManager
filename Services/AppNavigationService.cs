using System.Windows.Controls;
using UnturnedModManager.Pages;

namespace UnturnedModManager.Services;

public sealed record PageNavigationOrigin(Page Page, string BackLabel);

/// <summary>
/// 统一管理页面内的二级导航。所有详情页都从这里捕获真实来源并返回导航历史。
/// </summary>
public sealed class AppNavigationService
{
    public static AppNavigationService Current { get; } = new();

    private AppNavigationService() { }

    public bool OpenCommunityDetail(Page source, int modId)
    {
        var navigation = source.NavigationService;
        if (navigation is null) return false;
        var origin = new PageNavigationOrigin(source, GetBackLabel(source));
        return navigation.Navigate(new CommunityDetailPage(modId, origin));
    }

    public void ReturnToOrigin(Page currentPage, PageNavigationOrigin? origin)
    {
        var navigation = currentPage.NavigationService;
        if (navigation?.CanGoBack == true)
        {
            navigation.GoBack();
            return;
        }

        // 正常流程应始终走 GoBack；来源实例只用于导航历史被宿主清空后的恢复。
        if (origin is not null && navigation is not null)
        {
            navigation.Navigate(origin.Page);
            return;
        }

        navigation?.Navigate(new CommunityPage());
    }

    private static string GetBackLabel(Page source) => source switch
    {
        ModListPage => "返回本地插件",
        CommunityPage => "返回插件社区",
        _ => "返回上一级"
    };
}
