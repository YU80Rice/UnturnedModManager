using System;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace UnturnedModManager;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(AppSettings.ThemeMode);
        NavigationView.Navigate(typeof(Pages.HomePage));
    }

    /// <summary>
    /// 应用指定主题（Dark/Light），同步更新切换按钮的图标与文本。
    /// </summary>
    private void ApplyTheme(string themeMode)
    {
        bool isLight = themeMode.Equals("Light", StringComparison.OrdinalIgnoreCase);
        var theme = isLight ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica);

        if (ThemeToggleButtonIcon != null)
        {
            // 当前是浅色 -> 显示月亮图标（点击切回深色）；当前是深色 -> 显示太阳图标（点击切到浅色）
            ThemeToggleButtonIcon.Symbol = isLight
                ? SymbolRegular.WeatherMoon24
                : SymbolRegular.WeatherSunny24;
        }

        if (ThemeToggleText != null)
        {
            ThemeToggleText.Text = isLight ? "浅色模式" : "深色模式";
        }
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var currentMode = AppSettings.ThemeMode;
        var newMode = currentMode.Equals("Light", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        AppSettings.ThemeMode = newMode;
        ApplyTheme(newMode);
    }
}