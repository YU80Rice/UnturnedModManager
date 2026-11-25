using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace UnturnedModManager.Helpers;

/// <summary>
/// 玩家自定义壁纸辅助服务：提供非阻塞式安全图片加载引擎与有效性校验。
/// </summary>
public static class WallpaperHelper
{
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    private static readonly Lazy<DrawingImage> Graphite = new(() => CreatePreset("graphite"));
    private static readonly Lazy<DrawingImage> Mist = new(() => CreatePreset("mist"));
    private static readonly Lazy<DrawingImage> Ember = new(() => CreatePreset("ember"));

    /// <summary>显式本地图和内置预设优先；未选择时跟随主题包当前模式，最后回退纯色底。</summary>
    public static ImageSource? ResolveSource(string? localPath, string? themedPath, string? alternateThemedPath, string? preset)
    {
        if (LoadNonBlocking(localPath) is { } local) return local;
        if (GetPresetSource(preset) is { } builtIn) return builtIn;
        // 显式「纯净底色」：压过主题包自带的壁纸，只留 Layer0 纯色底。
        if (string.Equals(AppSettings.NormalizeWallpaperPreset(preset), "solid", StringComparison.Ordinal)) return null;
        return LoadNonBlocking(themedPath) ?? (string.Equals(themedPath, alternateThemedPath, StringComparison.OrdinalIgnoreCase)
            ? null : LoadNonBlocking(alternateThemedPath));
    }

    /// <summary>返回无需外部资源的冻结预设图；供壁纸宿主或设置预览复用。</summary>
    public static ImageSource? GetPresetSource(string? key) => AppSettings.NormalizeWallpaperPreset(key) switch
    {
        "graphite" => Graphite.Value,
        "mist" => Mist.Value,
        "ember" => Ember.Value,
        _ => null
    };

    private static DrawingImage CreatePreset(string key)
    {
        var viewport = new Rect(0, 0, 1280, 820);
        var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(viewport) };
        using (var drawing = group.Open())
        {
            switch (key)
            {
                case "graphite": // 原型：极光几何星云
                    drawing.DrawRectangle(Gradient(new Point(1, 1), "#0E1222", "#152B3B", "#1E4B5A", .50), null, viewport);
                    drawing.DrawEllipse(Paint("#257A52", .32), null, new Point(320, 240), 420, 180);
                    drawing.DrawEllipse(Paint("#3857D6", .26), null, new Point(920, 520), 460, 210);
                    drawing.DrawEllipse(Paint("#0078D4", .22), null, new Point(760, 200), 240, 240);
                    break;
                case "mist": // 原型：雾岚松林
                    drawing.DrawRectangle(Gradient(new Point(0, 1), "#1E352B", "#14231C", "#0D1512", .60), null, viewport);
                    drawing.DrawEllipse(Paint("#92D9B1", .16), null, new Point(260, 180), 200, 200);
                    drawing.DrawGeometry(Paint("#1B2A23", .85), null, Geometry.Parse("M0 540 L180 420 L340 530 L560 390 L790 520 L1020 370 L1280 510 L1280 820 L0 820 Z"));
                    drawing.DrawGeometry(Paint("#0D1411", .92), null, Geometry.Parse("M0 640 Q420 570 840 640 T1280 610 L1280 820 L0 820 Z"));
                    break;
                default: // 原型：PEI 篝火夕阳
                    drawing.DrawRectangle(Gradient(new Point(1, 1), "#1E2742", "#4A2F4A", "#C86416", .48), null, viewport);
                    drawing.DrawEllipse(Paint("#FFB67F", .28), null, new Point(940, 240), 150, 150);
                    drawing.DrawGeometry(Paint("#181715", .72), null, Geometry.Parse("M0 590 Q240 490 480 560 T980 520 L1280 570 L1280 820 L0 820 Z"));
                    drawing.DrawGeometry(Paint("#100F12", .88), null, Geometry.Parse("M0 660 Q320 580 680 650 T1280 620 L1280 820 L0 820 Z"));
                    break;
            }
        }
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static Brush Paint(string hex, double opacity) => new SolidColorBrush(ParseColor(hex)) { Opacity = opacity };

    private static Brush Gradient(Point end, string startColor, string middle, string endColor, double offset) =>
        new LinearGradientBrush(new GradientStopCollection
        {
            new(ParseColor(startColor), 0), new(ParseColor(middle), offset), new(ParseColor(endColor), 1)
        }, new Point(0, 0), end);

    private static Color ParseColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);

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
