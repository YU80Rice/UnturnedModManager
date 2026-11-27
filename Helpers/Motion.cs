using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;

namespace UnturnedModManager.Helpers;

public enum MotionSpeed { Press, Fast, Standard, Enter, Dialog, Exit }

public sealed class MotionState : DependencyObject
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.Register(
        nameof(Enabled), typeof(bool), typeof(MotionState), new PropertyMetadata(true));

    public bool Enabled
    {
        get => (bool)GetValue(EnabledProperty);
        private set => SetValue(EnabledProperty, value);
    }

    internal void Refresh() => Enabled = SystemParameters.ClientAreaAnimation;
}

public static class Motion
{
    public const int PressMs = 90;
    public const int FastMs = 140;
    public const int StandardMs = 180;
    public const int EnterMs = 200;
    public const int DialogMs = 220;
    public const int ExitMs = 120;

    private static readonly ConditionalWeakTable<Dispatcher, MotionState> States = new();
    private static readonly ConditionalWeakTable<UIElement, TransformState> Transforms = new();
    private static readonly ConditionalWeakTable<FrameworkElement, PressBehavior> PressBehaviors = new();

    static Motion() => SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;

    // 依赖对象归创建线程所有；每个 Dispatcher 首次取用时即刷新真实系统值。
    public static MotionState State => States.GetValue(Dispatcher.CurrentDispatcher, static _ =>
    {
        var state = new MotionState();
        state.Refresh();
        return state;
    });

    public static bool Enabled => State.Enabled;
    public static event EventHandler<PropertyChangedEventArgs>? StaticPropertyChanged;

    private static void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        foreach (var (dispatcher, state) in States)
        {
            if (dispatcher.CheckAccess()) state.Refresh();
            else if (!dispatcher.HasShutdownStarted)
                _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(state.Refresh));
        }
        StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(nameof(Enabled)));
    }

    public static TimeSpan T(MotionSpeed speed) => TimeSpan.FromMilliseconds(speed switch
    {
        MotionSpeed.Press => PressMs,
        MotionSpeed.Fast => FastMs,
        MotionSpeed.Standard => StandardMs,
        MotionSpeed.Enter => EnterMs,
        MotionSpeed.Dialog => DialogMs,
        MotionSpeed.Exit => ExitMs,
        _ => throw new ArgumentOutOfRangeException(nameof(speed))
    });

    public static void FadeIn(UIElement el, MotionSpeed speed = MotionSpeed.Standard, double from = 0) =>
        FadeInCore(el, speed, from, Enabled);

    public static void SlideFadeIn(UIElement el, double fromX = 0, double fromY = 8, MotionSpeed speed = MotionSpeed.Enter)
    {
        var animate = Enabled;
        var translate = FindTransform<TranslateTransform>(el, animate);
        if (translate is not null)
        {
            AnimateTransform(translate, TranslateTransform.XProperty, fromX, 0, speed, animate);
            AnimateTransform(translate, TranslateTransform.YProperty, fromY, 0, speed, animate);
        }
        FadeInCore(el, speed, 0, animate);
    }

    public static void ScaleFadeIn(UIElement el, double fromScale = 0.985, double originX = 0.5, double originY = 0.5,
        MotionSpeed speed = MotionSpeed.Dialog)
    {
        var animate = Enabled;
        var scale = FindTransform<ScaleTransform>(el, animate);
        if (scale is not null)
        {
            el.SetCurrentValue(UIElement.RenderTransformOriginProperty, new Point(originX, originY));
            AnimateTransform(scale, ScaleTransform.ScaleXProperty, fromScale, 1, speed, animate);
            AnimateTransform(scale, ScaleTransform.ScaleYProperty, fromScale, 1, speed, animate);
        }
        FadeInCore(el, speed, 0, animate);
    }

    public static void PressFeedback(FrameworkElement el) =>
        _ = PressBehaviors.GetValue(el, static element => new PressBehavior(element));

    private static void FadeInCore(UIElement el, MotionSpeed speed, double from, bool animate)
    {
        el.SetCurrentValue(UIElement.OpacityProperty, 1.0);
        el.BeginAnimation(UIElement.OpacityProperty, animate ? EnterAnimation(from, 1, speed) : null);
    }

    private static void AnimateTransform(Animatable target, DependencyProperty property, double from, double to,
        MotionSpeed speed, bool animate)
    {
        target.SetCurrentValue(property, to);
        target.BeginAnimation(property, animate && from != to ? EnterAnimation(from, to, speed) : null);
    }

    private static DoubleAnimation EnterAnimation(double from, double to, MotionSpeed speed) => new(from, to, T(speed))
    {
        EasingFunction = new CubicEase { EasingMode = speed == MotionSpeed.Exit ? EasingMode.EaseIn : EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };

    private static TTransform? FindTransform<TTransform>(UIElement el, bool create) where TTransform : Transform, new()
    {
        if (!Transforms.TryGetValue(el, out var state) || !ReferenceEquals(el.RenderTransform, state.Group))
        {
            if (!create) return null;
            var group = new TransformGroup();
            var original = el.RenderTransform;
            if (original is not null && !ReferenceEquals(original, Transform.Identity))
                group.Children.Add(original);
            el.SetCurrentValue(UIElement.RenderTransformProperty, group);
            state = new TransformState(group, original);
            Transforms.Remove(el);
            Transforms.Add(el, state);
        }

        foreach (var child in state.Group.Children)
            if (child is TTransform transform && !ReferenceEquals(transform, state.Original)) return transform;
        if (!create) return null;
        var added = new TTransform();
        state.Group.Children.Add(added);
        return added;
    }

    private sealed record TransformState(TransformGroup Group, Transform? Original);

    private sealed class PressBehavior
    {
        private static readonly DependencyPropertyDescriptor PressedProperty =
            DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase));

        private readonly FrameworkElement _element;

        public PressBehavior(FrameworkElement element)
        {
            _element = element;
            element.Unloaded += OnUnloaded;
            if (element is ButtonBase)
            {
                // IsPressed 同时覆盖鼠标与键盘；卸载时解除描述符订阅。
                element.Loaded += OnLoaded;
                if (element.IsLoaded) OnLoaded(element, new RoutedEventArgs());
            }
            else
            {
                element.PreviewMouseLeftButtonDown += OnMouseDown;
                element.PreviewMouseLeftButtonUp += OnMouseUp;
                element.MouseLeave += (_, _) => SetPressed(false);
                element.LostMouseCapture += (_, _) => SetPressed(false);
                element.IsEnabledChanged += (_, _) => SetPressed(false);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => PressedProperty.AddValueChanged(_element, OnPressedChanged);

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_element is ButtonBase) PressedProperty.RemoveValueChanged(_element, OnPressedChanged);
            SetPressed(false, immediate: true);
        }

        private void OnPressedChanged(object? sender, EventArgs e) => SetPressed(((ButtonBase)_element).IsPressed);
        private void OnMouseDown(object sender, MouseButtonEventArgs e) => SetPressed(true);
        private void OnMouseUp(object sender, MouseButtonEventArgs e) => SetPressed(false);

        private void SetPressed(bool pressed, bool immediate = false)
        {
            var animate = !immediate && Enabled;
            var scale = FindTransform<ScaleTransform>(_element, animate && pressed);
            if (scale is null) return;
            _element.SetCurrentValue(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
            var target = pressed && animate ? 0.97 : 1.0;
            AnimateAxis(ScaleTransform.ScaleXProperty);
            AnimateAxis(ScaleTransform.ScaleYProperty);

            void AnimateAxis(DependencyProperty property)
            {
                var current = (double)scale.GetValue(property);
                scale.SetCurrentValue(property, target);
                scale.BeginAnimation(property, animate && current != target ? new DoubleAnimation(current, target, T(MotionSpeed.Press))
                {
                    EasingFunction = new BackEase { Amplitude = 0.12, EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                } : null);
            }
        }
    }
}
