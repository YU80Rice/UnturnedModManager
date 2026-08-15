using UnturnedModManager.ViewModels;

namespace UnturnedModManager.Services;

/// <summary>
/// 将页面操作结果汇集到主窗口的右下角通知层。服务不保存消息，也不在后台执行操作；
/// 主窗口负责在 UI 线程展示并自动移除短暂提示。
/// </summary>
public sealed class UserNotificationService
{
    public event Action<UserNotice>? NoticePublished;

    public void Publish(UserNotice notice)
    {
        if (string.IsNullOrWhiteSpace(notice.Message))
            return;

        NoticePublished?.Invoke(notice);
    }
}

public sealed record ToastNotification(string Title, string Message, UserNoticeSeverity Severity)
{
    public static ToastNotification From(UserNotice notice) => new(
        notice.Severity switch
        {
            UserNoticeSeverity.Success => "操作完成",
            UserNoticeSeverity.Warning => "需要注意",
            UserNoticeSeverity.Error => "操作未完成",
            _ => "提示"
        },
        notice.Message,
        notice.Severity);
}
