using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UnturnedModManager.Models;
using UnturnedModManager.Services;

namespace UnturnedModManager.Windows;

public partial class ThemePackageImportWindow : Window
{
    private readonly ThemePackagePreview _preview;
    private readonly ThemePackageService _packageService;
    private readonly ThemeService _themeService;

    public ThemePackageOperationResult? Result { get; private set; }
    public bool AppliedImmediately { get; private set; }

    public ThemePackageImportWindow(
        ThemePackagePreview preview,
        ThemePackageService packageService,
        ThemeService themeService)
    {
        InitializeComponent();
        _preview = preview;
        _packageService = packageService;
        _themeService = themeService;

        var theme = preview.Theme;
        ThemeNameText.Text = string.IsNullOrWhiteSpace(theme.Name) ? "未命名主题" : theme.Name;
        ThemeAuthorText.Text = $"作者: {(string.IsNullOrWhiteSpace(theme.Author) ? "未知" : theme.Author)}";
        ThemeVersionText.Text = $"版本: {(string.IsNullOrWhiteSpace(theme.Version) ? "1.0.0" : theme.Version)}";
        ThemeBaseText.Text = $"基底: {(theme.BaseTheme == ThemePreference.Light ? "浅色模式" : "深色模式")}";
        ThemeDescriptionText.Text = string.IsNullOrWhiteSpace(theme.Description) ? "暂无说明信息。" : theme.Description;

        // Color Swatches
        SetSwatch(AccentSwatch, AccentHexText, theme.AccentColor, "#0078D4");
        SetSwatch(BgSwatch, BgHexText, theme.BackgroundColor, "#1E1E1E");
        SetSwatch(CardSwatch, CardHexText, theme.CardBackgroundColor, "#2D2D2D");

        CardOpacityText.Text = $"卡片不透明度: {Math.Round(theme.CardOpacity * 100)}%";
        CardRadiusText.Text = $"卡片圆角: {theme.CardBorderRadius} px";

        // Wallpaper Preview
        if (preview.WallpaperBytes is { Length: > 0 })
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(preview.WallpaperBytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                WallpaperImage.Source = bitmap;
                WallpaperImage.Visibility = Visibility.Visible;
                NoWallpaperText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                WallpaperImage.Visibility = Visibility.Collapsed;
                NoWallpaperText.Visibility = Visibility.Visible;
            }
        }
        else
        {
            WallpaperImage.Visibility = Visibility.Collapsed;
            NoWallpaperText.Visibility = Visibility.Visible;
        }
    }

    private static void SetSwatch(System.Windows.Controls.Border swatch, System.Windows.Controls.TextBlock label, string? hex, string fallback)
    {
        var targetHex = string.IsNullOrWhiteSpace(hex) ? fallback : hex;
        label.Text = targetHex;
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(targetHex)!;
            swatch.Background = new SolidColorBrush(color);
        }
        catch
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fallback)!;
            swatch.Background = new SolidColorBrush(color);
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _packageService.ImportPackage(_preview.PackagePath);
        if (Result.Success && Result.Theme is not null)
        {
            if (ApplyImmediatelyCheckBox.IsChecked == true)
            {
                _themeService.ApplyCustomTheme(Result.Theme, Result.WallpaperDestinationPath);
                AppliedImmediately = true;
            }
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show(
                this,
                $"导入主题包失败：{Result?.Message ?? "未知错误"}",
                "主题包导入错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
