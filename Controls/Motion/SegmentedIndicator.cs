using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UnturnedModManager.Helpers;
using Brush = System.Windows.Media.Brush;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MotionHelper = UnturnedModManager.Helpers.Motion;
using Panel = System.Windows.Controls.Panel;
using RadioButton = System.Windows.Controls.RadioButton;

namespace UnturnedModManager.Controls.Motion;

public static class SegmentedIndicator
{
    private static readonly ConditionalWeakTable<Panel, Indicator> Indicators = new();

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SegmentedIndicator), new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty PillBrushProperty = DependencyProperty.RegisterAttached(
        "PillBrush", typeof(Brush), typeof(SegmentedIndicator), new PropertyMetadata(null, OnAppearanceChanged));

    public static readonly DependencyProperty PillBorderBrushProperty = DependencyProperty.RegisterAttached(
        "PillBorderBrush", typeof(Brush), typeof(SegmentedIndicator), new PropertyMetadata(null, OnAppearanceChanged));

    public static readonly DependencyProperty PillBorderThicknessProperty = DependencyProperty.RegisterAttached(
        "PillBorderThickness", typeof(Thickness), typeof(SegmentedIndicator), new PropertyMetadata(new Thickness(0), OnAppearanceChanged));

    public static readonly DependencyProperty PillCornerRadiusProperty = DependencyProperty.RegisterAttached(
        "PillCornerRadius", typeof(CornerRadius), typeof(SegmentedIndicator), new PropertyMetadata(new CornerRadius(14), OnAppearanceChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static Brush? GetPillBrush(DependencyObject element) => (Brush?)element.GetValue(PillBrushProperty);
    public static void SetPillBrush(DependencyObject element, Brush? value) => element.SetValue(PillBrushProperty, value);
    public static Brush? GetPillBorderBrush(DependencyObject element) => (Brush?)element.GetValue(PillBorderBrushProperty);
    public static void SetPillBorderBrush(DependencyObject element, Brush? value) => element.SetValue(PillBorderBrushProperty, value);
    public static Thickness GetPillBorderThickness(DependencyObject element) => (Thickness)element.GetValue(PillBorderThicknessProperty);
    public static void SetPillBorderThickness(DependencyObject element, Thickness value) => element.SetValue(PillBorderThicknessProperty, value);
    public static CornerRadius GetPillCornerRadius(DependencyObject element) => (CornerRadius)element.GetValue(PillCornerRadiusProperty);
    public static void SetPillCornerRadius(DependencyObject element, CornerRadius value) => element.SetValue(PillCornerRadiusProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel) return;
        if (e.NewValue is true)
        {
            _ = Indicators.GetValue(panel, static host => new Indicator(host));
        }
        else if (Indicators.TryGetValue(panel, out var indicator))
        {
            indicator.Detach();
            Indicators.Remove(panel);
        }
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Panel panel && Indicators.TryGetValue(panel, out var indicator))
            indicator.UpdateAppearance();
    }

    private sealed class Indicator
    {
        private readonly Panel _host;
        private readonly Border _pill = new()
        {
            Opacity = 0,
            Width = 0,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        private Window? _window;
        private RadioButton? _selected;
        private (double X, double Width)? _target;
        private bool _selectionPending = true;
        private bool _snap = true;
        private DispatcherOperation? _selectionUpdate;

        public Indicator(Panel host)
        {
            _host = host;
            Panel.SetZIndex(_pill, -1);
            host.Children.Insert(0, _pill);
            UpdateAppearance();
            host.Loaded += OnLoaded;
            host.Unloaded += OnUnloaded;
            host.SizeChanged += OnHostSizeChanged;
            host.AddHandler(RadioButton.CheckedEvent, new RoutedEventHandler(OnSelectionChanged), true);
            host.AddHandler(RadioButton.UncheckedEvent, new RoutedEventHandler(OnSelectionChanged), true);
            if (host.IsLoaded) AttachLayout();
        }

        public void UpdateAppearance()
        {
            if (GetPillBrush(_host) is { } fill) _pill.Background = fill;
            else _pill.SetResourceReference(Border.BackgroundProperty, "AppPillSelectedBrush");
            _pill.BorderBrush = GetPillBorderBrush(_host);
            _pill.BorderThickness = GetPillBorderThickness(_host);
            _pill.CornerRadius = GetPillCornerRadius(_host);
        }

        public void Detach()
        {
            DetachLayout();
            _host.Loaded -= OnLoaded;
            _host.Unloaded -= OnUnloaded;
            _host.SizeChanged -= OnHostSizeChanged;
            _host.RemoveHandler(RadioButton.CheckedEvent, new RoutedEventHandler(OnSelectionChanged));
            _host.RemoveHandler(RadioButton.UncheckedEvent, new RoutedEventHandler(OnSelectionChanged));
            _host.Children.Remove(_pill);
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => AttachLayout();
        private void OnUnloaded(object sender, RoutedEventArgs e) => DetachLayout();

        private void AttachLayout()
        {
            _selectionPending = _snap = true;
            _host.LayoutUpdated += OnLayoutUpdated;
            _window = Window.GetWindow(_host);
            if (_window is not null) _window.SizeChanged += OnWindowSizeChanged;
            Update();
        }

        private void DetachLayout()
        {
            _host.LayoutUpdated -= OnLayoutUpdated;
            if (_window is not null) _window.SizeChanged -= OnWindowSizeChanged;
            _window = null;
            _selectionUpdate?.Abort();
            _selectionUpdate = null;
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not RadioButton) return;
            _selectionPending = true;
            if (!_host.IsLoaded || _selectionUpdate is not null) return;
            _selectionUpdate = _host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _selectionUpdate = null;
                if (_selectionPending) Update();
            }));
        }

        private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 选中文字字重变化也可能改变容器宽度，此时保留切换动画。
            if (!_selectionPending) _snap = true;
        }

        private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e) => _snap = true;
        private void OnLayoutUpdated(object? sender, EventArgs e) => Update();

        private void Update()
        {
            if (!_host.IsArrangeValid) return;
            var previous = _selected;
            // Checked / Unchecked 在一次布局内合并；避免切换时短暂清空胶囊。
            var selected = _selectionPending ? FindChecked(_host) : _selected;
            if (selected is not null && !selected.IsArrangeValid) return;
            _selected = selected;
            _selectionPending = false;
            if (selected is null)
            {
                if (_target.HasValue)
                {
                    _pill.BeginAnimation(UIElement.OpacityProperty, null);
                    _pill.Opacity = 0;
                }
                _target = null;
                _snap = false;
                return;
            }
            if (_host is Grid grid)
            {
                var span = Math.Max(1, grid.ColumnDefinitions.Count);
                if (Grid.GetColumnSpan(_pill) != span) Grid.SetColumnSpan(_pill, span);
            }
            var next = (X: selected.TranslatePoint(default, _host).X, Width: selected.ActualWidth);
            if (_target == next && !_snap) return;
            var animate = !_snap && MotionHelper.Enabled;
            var slide = animate && _target.HasValue && !ReferenceEquals(previous, _selected);
            var fade = animate && !_target.HasValue;
            _target = next;
            _snap = false;

            var fromMargin = _pill.Margin;
            var fromWidth = _pill.Width;
            // 抵消占用宽度，使垫底的胶囊不参与槽位测量，即使徽标消失、容器变窄也不会撑大布局。
            var margin = new Thickness(next.X, 0, -(next.X + next.Width + 1), 0);
            _pill.Margin = margin;
            _pill.Width = next.Width;
            var duration = MotionHelper.T(MotionSpeed.Standard);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            _pill.BeginAnimation(FrameworkElement.MarginProperty, slide ? new ThicknessAnimation(fromMargin, margin, duration)
            { EasingFunction = easing, FillBehavior = FillBehavior.Stop } : null);
            _pill.BeginAnimation(FrameworkElement.WidthProperty, slide ? new DoubleAnimation(fromWidth, next.Width, duration)
            { EasingFunction = easing, FillBehavior = FillBehavior.Stop } : null);
            if (fade) MotionHelper.FadeIn(_pill, MotionSpeed.Press);
            else if (!animate)
            {
                _pill.BeginAnimation(UIElement.OpacityProperty, null);
                _pill.Opacity = 1;
            }
        }

        private static RadioButton? FindChecked(Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is RadioButton { IsChecked: true, Visibility: Visibility.Visible } radio) return radio;
                if (child is Panel nested && FindChecked(nested) is { } selected) return selected;
            }
            return null;
        }
    }
}
