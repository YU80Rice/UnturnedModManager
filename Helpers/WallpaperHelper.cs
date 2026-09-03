using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace UnturnedModManager.Helpers;

/// <summary>
/// 玩家自定义壁纸辅助服务：提供非阻塞式安全图片加载引擎与有效性校验。
/// </summary>
public static class WallpaperHelper
{
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// 安全、非阻塞地从本地磁盘加载图片为 BitmapSource。
    /// 机制：读取全量字节进内存并构造 MemoryStream 后 Freeze()，加载后立即释放底层文件句柄，杜绝锁死玩家本地文件。
    /// 若路径无效、文件不存在或字节损坏，安全返回 null。
    /// </summary>
    public static BitmapSource? LoadNonBlocking(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        try
        {
            if (!File.Exists(filePath)) return null;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) return null;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0 || fileInfo.Length > MaxFileSizeBytes) return null;

            var bytes = File.ReadAllBytes(filePath);
            using var ms = new MemoryStream(bytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            // 异常破损图片或权限异常时安全降级
            return null;
        }
    }

    /// <summary>
    /// 检查指定路径是否为合法、可成功解析的图片文件。
    /// </summary>
    public static bool IsValidImageFile(string? filePath)
    {
        return LoadNonBlocking(filePath) is not null;
    }
}
