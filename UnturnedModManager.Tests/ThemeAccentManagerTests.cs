using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using UnturnedModManager.Models;
using UnturnedModManager.Services;
using Wpf.Ui.Appearance;
using Xunit;

namespace UnturnedModManager.Tests;

[Collection("WpfThemeCollection")]
public sealed class ThemeAccentManagerTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
        {
            throw exception;
        }
    }

    [Fact]
    public void CaptureShellPalettePreviews_WhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("UMM_SHELL_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;

        RunOnStaThread(() =>
        {
            // Run this filter by itself so the WPF application belongs to this STA.
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var theme = new ThemeService();
            System.IO.Directory.CreateDirectory(output);
            const int width = 1280, height = 820;
            var window = new MainWindow();
            var layout = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
            var frame = Assert.IsType<System.Windows.Controls.Frame>(window.FindName("PageHost"));
            var launch = Assert.IsAssignableFrom<System.Windows.Controls.Button>(window.FindName("LaunchButton"));
            launch.IsEnabled = true;
            var tab = Assert.IsType<System.Windows.Controls.RadioButton>(window.FindName("PlayTab"));
            tab.IsChecked = true;
            var wheel = new Controls.ColorWheelControl { IsExpanded = true, RingSize = 216 };
            var card = new System.Windows.Controls.Border
            {
                Width = 900, Padding = new Thickness(24), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = wheel
            };
            card.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "ControlFillColorDefaultBrush");
            card.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
            frame.Content = card;

            foreach (var mode in new[] { ThemePreference.Dark, ThemePreference.Light })
            foreach (var hex in new[] { "#0078D4", "#FF0000", "#FFFF00", "#330066", "#000000", "#FFFFFF" })
            {
                theme.Apply(mode, persist: false);
                theme.ApplyAccent(hex, 16, persist: false);
                wheel.IsDarkTheme = mode == ThemePreference.Dark;
                wheel.AccentColor = (Color)ColorConverter.ConvertFromString(hex)!;
                layout.Measure(new Size(width, height));
                layout.Arrange(new Rect(0, 0, width, height));
                layout.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(layout);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var stream = System.IO.File.Create(System.IO.Path.Combine(output, $"shell-{mode.ToString().ToLowerInvariant()}-{hex[1..]}.png"));
                encoder.Save(stream);
            }
        });
    }

    [Fact]
    public void ApplyCustomTheme_SynchronizesWpfUiAccentColorAndApplicationResources()
    {
        RunOnStaThread(() =>
        {
            var app = Application.Current ?? new Application();
            var themeService = new ThemeService();
            var pinkTheme = new CustomTheme
            {
                Name = "SakuraPink",
                AccentColor = "#FF69B4",
                BackgroundColor = "#1D161A",
                CardBackgroundColor = "#291F25",
                BaseTheme = ThemePreference.Dark
            };

            themeService.ApplyCustomTheme(pinkTheme, null, null, ThemePreference.Dark, persist: false);

            var accentColor = (Color)ColorConverter.ConvertFromString("#FF69B4")!;
            Assert.Equal("#FF69B4", themeService.CurrentAccentHex);
            AssertAccentResources(app, accentColor);

            themeService.Apply(ThemePreference.Light, persist: false);
            AssertAccentResources(app, accentColor);
        });
    }

    [Theory]
    [InlineData(ThemePalette.Fluent, "#0078D4")]
    [InlineData(ThemePalette.MascotOrange, "#E8722C")]
    public void ApplyPalette_DerivesAndSynchronizesMigratedAccent(ThemePalette palette, string expectedHex)
    {
        RunOnStaThread(() =>
        {
            var app = Application.Current ?? new Application();
            var themeService = new ThemeService();
            themeService.Apply(ThemePreference.Dark, persist: false);
            themeService.ApplyAccent("#FF69B4", 24, persist: false);

            themeService.ApplyPalette(palette, persist: false);

            var expected = (Color)ColorConverter.ConvertFromString(expectedHex)!;
            Assert.Equal(expectedHex, themeService.CurrentAccentHex);
            Assert.Equal(24d, themeService.CurrentSurfaceTint);
            AssertAccentResources(app, expected);
            Assert.Equal(expected, Assert.IsType<SolidColorBrush>(app.Resources["AppSemanticAccentBrush"]).Color);
            Assert.Equal(expected, Assert.IsType<SolidColorBrush>(app.Resources["AppPillSelectedBrush"]).Color);
            var foreground = Assert.IsType<SolidColorBrush>(app.Resources["TextOnAccentFillColorPrimaryBrush"]).Color;
            Assert.True(Helpers.ColorContrastHelper.GetContrastRatio(foreground, expected) >= 4.5);
        });
    }

    private static void AssertAccentResources(Application app, Color accent)
    {
        Assert.Equal(accent, ApplicationAccentColorManager.SystemAccent);
        Assert.Equal(accent, ApplicationAccentColorManager.PrimaryAccent);
        Assert.Equal(accent, Assert.IsType<Color>(app.Resources["SystemAccentColorPrimary"]));
        Assert.Equal(accent, Assert.IsType<SolidColorBrush>(app.Resources["AccentFillColorDefaultBrush"]).Color);
        Assert.Equal(ApplicationAccentColorManager.SecondaryAccent,
            Assert.IsType<SolidColorBrush>(app.Resources["AccentFillColorSecondaryBrush"]).Color);
        Assert.Equal(ApplicationAccentColorManager.TertiaryAccent,
            Assert.IsType<SolidColorBrush>(app.Resources["AccentFillColorTertiaryBrush"]).Color);
    }
}
