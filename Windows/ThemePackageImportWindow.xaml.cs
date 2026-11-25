using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;
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
        theme.EnsureDualMode();
        ThemeNameText.Text = string.IsNullOrWhiteSpace(theme.Name) ? "未命名主题" : theme.Name;
        ThemeAuthorText.Text = $"作者: {(string.IsNullOrWhiteSpace(theme.Author) ? "未知" : theme.Author)}";
        ThemeVersionText.Text = $"版本: {(string.IsNullOrWhiteSpace(theme.Version) ? "1.0.0" : theme.Version)}";
        ThemeBaseText.Text = $"基底: {(theme.BaseTheme == ThemePreference.Light ? "浅色模式" : "深色模式")}";
        ThemeDescriptionText.Text = string.IsNullOrWhiteSpace(theme.Description) ? "暂无说明信息。" : theme.Description;

        SetPreviewMode(_themeService.AppliedTheme);
    }

    public ThemePreference CurrentPreviewMode { get; private set; } = ThemePreference.Dark;

    public void SetPreviewMode(ThemePreference mode)
    {
        CurrentPreviewMode = mode;
        var isLight = mode == ThemePreference.Light;
        PreviewDayButton.Appearance = isLight ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
        PreviewNightButton.Appearance = isLight ? Wpf.Ui.Controls.ControlAppearance.Secondary : Wpf.Ui.Controls.ControlAppearance.Primary;

        var theme = _preview.Theme;
        theme.EnsureDualMode();

        var fallbackAccent = ThemeService.ParseColor(AppSettings.DefaultAccentHex);
        var accent = ReadSwatchColor(theme.AccentColor, fallbackAccent);
        var derived = ThemeService.DeriveColors(accent, !isLight, _themeService.CurrentSurfaceTint);
        SetSwatch(AccentSwatch, AccentHexText, accent);
        SetSwatch(BgSwatch, BgHexText, ReadSwatchColor(
            isLight ? theme.LightBackgroundColor : theme.BackgroundColor, derived["AppSemanticWindowBackgroundBrush"]));
        SetSwatch(CardSwatch, CardHexText, ReadSwatchColor(
            isLight ? theme.LightCardBackgroundColor : theme.CardBackgroundColor, derived["AppSemanticCardSurfaceBrush"]));

        if (isLight)
        {


            var opacity = theme.LightCardOpacity ?? theme.CardOpacity;
            var radius = theme.LightCardBorderRadius ?? theme.CardBorderRadius;
            CardOpacityText.Text = $"卡片不透明度: {Math.Round(opacity * 100)}%";
            CardRadiusText.Text = $"卡片圆角: {radius} px";
            UpdateWallpaperPreview(_preview.WallpaperLightBytes ?? _preview.WallpaperDarkBytes);
        }
        else
        {


            CardOpacityText.Text = $"卡片不透明度: {Math.Round(theme.CardOpacity * 100)}%";
            CardRadiusText.Text = $"卡片圆角: {theme.CardBorderRadius} px";
            UpdateWallpaperPreview(_preview.WallpaperDarkBytes ?? _preview.WallpaperLightBytes);
        }
    }

    private void PreviewNightButton_Click(object sender, RoutedEventArgs e) =>
        SetPreviewMode(ThemePreference.Dark);

    private void PreviewDayButton_Click(object sender, RoutedEventArgs e) =>
        SetPreviewMode(ThemePreference.Light);

    private void UpdateWallpaperPreview(byte[]? bytes)
    {
        if (bytes is { Length: > 0 })
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(bytes);
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

    private static MediaColor ReadSwatchColor(string? hex, MediaColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return ThemeService.ParseColor(hex); }
        catch (FormatException) { return fallback; }
        catch (NotSupportedException) { return fallback; }
    }

    private static void SetSwatch(System.Windows.Controls.Border swatch, System.Windows.Controls.TextBlock label, MediaColor color)
    {
        label.Text = color.A == byte.MaxValue ? ThemeService.ToHex(color) : color.ToString();
        swatch.Background = new SolidColorBrush(color);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _packageService.ImportPackage(_preview.PackagePath);
        if (Result.Success && Result.Theme is not null)
        {
            if (ApplyImmediatelyCheckBox.IsChecked == true)
            {
                _themeService.ApplyCustomTheme(
                    Result.Theme,
                    Result.WallpaperDestinationPath,
                    Result.WallpaperLightDestinationPath,
                    targetMode: CurrentPreviewMode);
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
