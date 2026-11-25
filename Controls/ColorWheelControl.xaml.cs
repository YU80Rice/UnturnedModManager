using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UnturnedModManager.Helpers;
using UnturnedModManager.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Rectangle = System.Windows.Shapes.Rectangle;
using UserControl = System.Windows.Controls.UserControl;

namespace UnturnedModManager.Controls;

public partial class ColorWheelControl : UserControl
{
    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(ColorWheelControl),
        new FrameworkPropertyMetadata(Color.FromRgb(0, 120, 212), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AccentChanged,
            (_, value) => { var c = (Color)value; return Color.FromRgb(c.R, c.G, c.B); }));
    public static readonly DependencyProperty SurfaceTintProperty = DependencyProperty.Register(
        nameof(SurfaceTint), typeof(double), typeof(ColorWheelControl),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Refresh,
            (_, value) => AppSettings.NormalizeSurfaceTint((double)value)));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(ColorWheelControl), new FrameworkPropertyMetadata(false, Refresh));
    public static readonly DependencyProperty RingSizeProperty = DependencyProperty.Register(
        nameof(RingSize), typeof(double), typeof(ColorWheelControl), new FrameworkPropertyMetadata(124d, Refresh),
        value => value is double size && double.IsFinite(size) && size > 0);
    public static readonly DependencyProperty IsDarkThemeProperty = DependencyProperty.Register(
        nameof(IsDarkTheme), typeof(bool), typeof(ColorWheelControl), new FrameworkPropertyMetadata(true, Refresh));

    public static readonly RoutedEvent AccentColorChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(AccentColorChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<Color>), typeof(ColorWheelControl));
    public event RoutedPropertyChangedEventHandler<Color>? AccentColorChanged
    {
        add => AddHandler(AccentColorChangedEvent, value);
        remove => RemoveHandler(AccentColorChangedEvent, value);
    }

    public Color AccentColor { get => (Color)GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }
    public double SurfaceTint { get => (double)GetValue(SurfaceTintProperty); set => SetValue(SurfaceTintProperty, value); }
    public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public double RingSize { get => (double)GetValue(RingSizeProperty); set => SetValue(RingSizeProperty, value); }
    public bool IsDarkTheme { get => (bool)GetValue(IsDarkThemeProperty); set => SetValue(IsDarkThemeProperty, value); }

    private static readonly Brush HueBrush = CreateHueBrush();
    private readonly List<(Rectangle Chip, TextBlock Hex, string Key)> _swatches = [];
    private bool _ready;
    private bool _updating;
    private bool _editingHex;
    private bool _writingHsv;
    private double _h, _s, _v;

    public ColorWheelControl()
    {
        InitializeComponent();
        HueRing.Fill = HueBrush;
        AddSwatch("强调主色", "AppSemanticAccentBrush");
        AddSwatch("悬浮高亮", "AppSemanticHoverBrush");
        AddSwatch("文字高亮", "AppSemanticTextHighlightBrush");
        AddSwatch("窗口底色", "AppSemanticWindowBackgroundBrush");
        AddSwatch("卡片表面", "AppSemanticCardSurfaceBrush");
        _ready = true;
        (_h, _s, _v) = ThemeService.ToHsv(AccentColor);
        RefreshEditor();
    }

    private static void AccentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var wheel = (ColorWheelControl)sender;
        if (!wheel._writingHsv) (wheel._h, wheel._s, wheel._v) = ThemeService.ToHsv((Color)args.NewValue);
        wheel.RefreshEditor();
        wheel.RaiseEvent(new RoutedPropertyChangedEventArgs<Color>((Color)args.OldValue, (Color)args.NewValue, AccentColorChangedEvent));
    }

    private static void Refresh(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((ColorWheelControl)sender).RefreshEditor();

    private void RefreshEditor()
    {
        if (!_ready || _updating) return;
        _updating = true;
        try
        {
            var color = AccentColor;
            var hex = ThemeService.ToHex(color);
            var tokens = ThemeService.DeriveColors(color, IsDarkTheme, SurfaceTint);
            var onAccent = tokens["TextOnAccentFillColorPrimaryBrush"];
            var ratio = ColorContrastHelper.GetContrastRatio(color, onAccent);
            var grade = ratio >= 7 ? "AAA" : ratio >= 4.5 ? "AA" : "低于 AA";
            ContrastText.Text = $"文本对比 {ratio.ToString("0.0", CultureInfo.InvariantCulture)}:1 ({grade})";
            ContrastBadge.Background = BrushFor(tokens[ratio >= 7 ? "AppSuccessSurfaceBrush" : ratio >= 4.5 ? "AppAccentSoftBrush" : "AppWarningSurfaceBrush"]);
            ContrastText.Foreground = BrushFor(tokens[ratio >= 7 ? "AppSuccessTextBrush" : ratio >= 4.5 ? "AppSemanticTextHighlightBrush" : "AppWarningTextBrush"]);
            Wheel.Width = Wheel.Height = IsExpanded ? RingSize : RingSize >= 176 ? 124 : RingSize;
            var visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
            var inverse = IsExpanded ? Visibility.Collapsed : Visibility.Visible;
            Advanced.Visibility = SvPad.Visibility = HexBox.Visibility = visibility;
            CenterButton.Visibility = CollapsedHex.Visibility = ColorSummary.Visibility = RingHint.Visibility = inverse;
            ExpandButton.Content = IsExpanded ? "收起滑块" : "展开完整滑块";
            CenterButton.Background = HuePuck.Fill = SvPuck.Fill = BrushFor(color);
            CenterButton.Foreground = BrushFor(onAccent);
            CenterHex.Text = CollapsedHex.Text = hex;
            if (!_editingHex && HexBox.Text != hex) HexBox.Text = hex;
            ColorSummary.Text = $"色相 {_h:0}° · 饱和 {_s:0}% · 明度 {_v:0}% · 染色 {SurfaceTint:0}%";
            HueSlider.Value = Math.Min(359, _h);
            SaturationSlider.Value = _s;
            ValueSlider.Value = _v;
            TintSlider.Value = SurfaceTint;
            var pos = At(_h, 99);
            Canvas.SetLeft(HuePuck, pos.X - 8.5); Canvas.SetTop(HuePuck, pos.Y - 8.5);
            Canvas.SetLeft(SvPuck, _s * 1.10 - 7.5); Canvas.SetTop(SvPuck, (100 - _v) * 1.10 - 7.5);
            SvHue.Fill = new LinearGradientBrush(Colors.White, ThemeService.FromHsv(_h, 100, 100), 0);
            foreach (var (chip, label, key) in _swatches)
            {
                chip.Fill = BrushFor(tokens[key]);
                label.Text = ThemeService.ToHex(tokens[key]);
            }
        }
        finally { _updating = false; }
    }

    private void AddSwatch(string title, string key)
    {
        var chip = new Rectangle { Height = 8, RadiusX = 3, RadiusY = 3 };
        var hex = new TextBlock { FontSize = 10, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
        var panel = new StackPanel { MinWidth = 58 };
        panel.Children.Add(chip);
        panel.Children.Add(new TextBlock { Text = title, FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
        panel.Children.Add(hex);
        var border = new Border { Child = panel, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(5), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1) };
        border.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        border.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
        Swatches.Children.Add(border);
        _swatches.Add((chip, hex, key));
    }

    private void ToggleExpanded(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsExpandedProperty, !IsExpanded);
        ExpandButton.Focus();
    }
    private void Reset(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(SurfaceTintProperty, 16d);
        SetCurrentValue(AccentColorProperty, Color.FromRgb(0, 120, 212));
        if (!_updating) RefreshEditor();
    }
    private void HexChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready || _updating || AppSettings.NormalizeAccentHex(HexBox.Text) is not { } normalized) return;
        _editingHex = true;
        try { SetCurrentValue(AccentColorProperty, ThemeService.ParseColor(normalized)); }
        finally { _editingHex = false; }
    }
    private void HexLostFocus(object sender, KeyboardFocusChangedEventArgs e) => RefreshEditor();
    private void HsvSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _updating) return;
        if (ReferenceEquals(sender, HueSlider)) _h = e.NewValue;
        else if (ReferenceEquals(sender, SaturationSlider)) _s = e.NewValue;
        else _v = e.NewValue;
        WriteHsv();
    }
    private void TintSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_ready && !_updating) SetCurrentValue(SurfaceTintProperty, e.NewValue);
    }
    private void HueDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(HueRing);
        var r = Math.Sqrt(Math.Pow(p.X - 108, 2) + Math.Pow(p.Y - 108, 2));
        if (r < 87) return;
        HueRing.Focus();
        if (HueRing.CaptureMouse()) UpdateHue(p);
        e.Handled = true;
    }
    private void HueMove(object sender, MouseEventArgs e) { if (HueRing.IsMouseCaptured) UpdateHue(e.GetPosition(HueRing)); }
    private void HueUp(object sender, MouseButtonEventArgs e) { if (HueRing.IsMouseCaptured) HueRing.ReleaseMouseCapture(); }
    private void HueKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space) ToggleExpanded(sender, e);
        else if (e.Key is Key.Left or Key.Right)
        {
            var step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            SetHue((_h + (e.Key == Key.Left ? -step : step) + 360) % 360);
        }
        else return;
        e.Handled = true;
    }
    private void UpdateHue(System.Windows.Point p) => SetHue((Math.Atan2(p.Y - 108, p.X - 108) * 180 / Math.PI + 360) % 360);
    private void SetHue(double hue)
    {
        _h = hue;
        if (!IsExpanded && (_s < 8 || _v < 16)) { _s = Math.Max(_s, 78); _v = Math.Max(_v, 84); }
        WriteHsv();
    }
    private void SvDown(object sender, MouseButtonEventArgs e) { if (SvPad.CaptureMouse()) UpdateSv(e.GetPosition(SvPad)); e.Handled = true; }
    private void SvMove(object sender, MouseEventArgs e) { if (SvPad.IsMouseCaptured) UpdateSv(e.GetPosition(SvPad)); }
    private void SvUp(object sender, MouseButtonEventArgs e) { if (SvPad.IsMouseCaptured) SvPad.ReleaseMouseCapture(); }
    private void UpdateSv(System.Windows.Point p)
    {
        _s = Math.Clamp(p.X / 110, 0, 1) * 100;
        _v = (1 - Math.Clamp(p.Y / 110, 0, 1)) * 100;
        WriteHsv();
    }
    private void WriteHsv()
    {
        _writingHsv = true;
        try { SetCurrentValue(AccentColorProperty, ThemeService.FromHsv(_h, _s, _v)); }
        finally { _writingHsv = false; }
        RefreshEditor();
    }

    private static Brush BrushFor(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
    private static System.Windows.Point At(double hue, double radius)
    {
        var angle = hue * Math.PI / 180;
        return new(108 + Math.Cos(angle) * radius, 108 + Math.Sin(angle) * radius);
    }
    private static Brush CreateHueBrush()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 216, 216));
            for (int degree = 0; degree < 360; degree++)
            {
                var geometry = new StreamGeometry();
                using (var segment = geometry.Open())
                {
                    segment.BeginFigure(At(degree - .5, 108), true, true);
                    segment.ArcTo(At(degree + 1.5, 108), new System.Windows.Size(108, 108), 0, false, SweepDirection.Clockwise, true, false);
                    segment.LineTo(At(degree + 1.5, 88), true, false);
                    segment.ArcTo(At(degree - .5, 88), new System.Windows.Size(88, 88), 0, false, SweepDirection.Counterclockwise, true, false);
                }
                geometry.Freeze();
                context.DrawGeometry(BrushFor(ThemeService.FromHsv(degree, 100, 100)), null, geometry);
            }
        }
        var brush = new DrawingBrush(drawing);
        brush.Freeze();
        return brush;
    }
}
