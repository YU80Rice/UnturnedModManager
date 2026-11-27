using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using UnturnedModManager.Helpers;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Control = System.Windows.Controls.Control;
using MotionHelper = UnturnedModManager.Helpers.Motion;
using Rectangle = System.Windows.Shapes.Rectangle;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;

namespace UnturnedModManager.Controls.Motion;

public static class ToggleSwitchMotion
{
    private static readonly ConditionalWeakTable<Control, Behavior> Behaviors = new();

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ToggleSwitchMotion), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject? element) => (bool?)element?.GetValue(IsEnabledProperty) ?? false;
    public static void SetIsEnabled(DependencyObject? element, bool value) => element?.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Control control || control is not ToggleSwitch) return;
        if (e.NewValue is true)
        {
            var behavior = Behaviors.GetValue(control, static target => new Behavior(target));
            behavior.Attach();
        }
        else if (Behaviors.TryGetValue(control, out var behavior))
        {
            Behaviors.Remove(control);
            behavior.Detach();
        }
    }

    private sealed record Parts(
        TranslateTransform OffThumb, TranslateTransform OnThumb,
        Rectangle OffTrack, Rectangle OnTrack, ScaleTransform Scale);

    private sealed class Behavior(Control control)
    {
        private static readonly DependencyPropertyDescriptor CheckedProperty =
            DependencyPropertyDescriptor.FromProperty(ToggleButton.IsCheckedProperty, typeof(ToggleSwitch));
        private static readonly DependencyPropertyDescriptor PressedProperty =
            DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ToggleSwitch));
        private static readonly DependencyPropertyDescriptor TemplateProperty =
            DependencyPropertyDescriptor.FromProperty(Control.TemplateProperty, typeof(Control));
        private static readonly DependencyPropertyDescriptor MotionEnabledProperty =
            DependencyPropertyDescriptor.FromProperty(MotionState.EnabledProperty, typeof(MotionState));

        private Parts? _parts;
        private MotionState? _motionState;
        private DispatcherOperation? _templateRefresh;
        private bool _attached;
        private bool _listening;
        private int _checkedRevision;

        private bool IsChecked => control.GetValue(ToggleButton.IsCheckedProperty) is true;
        private bool IsPressed => control.GetValue(ButtonBase.IsPressedProperty) is true;

        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            control.Loaded += OnLoaded;
            control.Unloaded += OnUnloaded;
            if (control.IsLoaded) StartListening();
        }

        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            control.Loaded -= OnLoaded;
            control.Unloaded -= OnUnloaded;
            StopListening();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => StartListening();
        private void OnUnloaded(object sender, RoutedEventArgs e) => StopListening();

        private void StartListening()
        {
            if (!_attached || _listening) return;
            _listening = true;
            CheckedProperty.AddValueChanged(control, OnCheckedChanged);
            PressedProperty.AddValueChanged(control, OnPressedChanged);
            TemplateProperty.AddValueChanged(control, OnTemplateChanged);
            _motionState = MotionHelper.State;
            MotionEnabledProperty.AddValueChanged(_motionState, OnMotionEnabledChanged);
            RefreshParts();
        }

        private void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            _templateRefresh?.Abort();
            _templateRefresh = null;
            CheckedProperty.RemoveValueChanged(control, OnCheckedChanged);
            PressedProperty.RemoveValueChanged(control, OnPressedChanged);
            TemplateProperty.RemoveValueChanged(control, OnTemplateChanged);
            if (_motionState is { } state)
                MotionEnabledProperty.RemoveValueChanged(state, OnMotionEnabledChanged);
            _motionState = null;
            ReleaseParts();
        }

        private void OnTemplateChanged(object? sender, EventArgs e)
        {
            if (!_listening) return;
            ReleaseParts();
            _templateRefresh ??= control.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _templateRefresh = null;
                if (_listening) RefreshParts();
            }));
        }

        private void RefreshParts()
        {
            control.ApplyTemplate();
            if (!_listening || control.Template is not { } template ||
                template.FindName("SwitchTrack", control) is not FrameworkElement ||
                template.FindName("ToggleEllipse", control) is not Ellipse { RenderTransform: TranslateTransform offThumb } offEllipse ||
                template.FindName("ActiveToggleEllipse", control) is not Ellipse { RenderTransform: TranslateTransform onThumb } onEllipse ||
                template.FindName("ToggleRectangle", control) is not Rectangle offTrack ||
                template.FindName("ActiveToggleRectangle", control) is not Rectangle onTrack ||
                template.FindName("RootGrid", control) is not System.Windows.Controls.Grid { RenderTransform: ScaleTransform scale } root)
                return;

            // 模板可以共享冻结的变换；只修改当前实例的变换对象。
            if (offThumb.IsFrozen) offEllipse.RenderTransform = offThumb = offThumb.Clone();
            if (onThumb.IsFrozen) onEllipse.RenderTransform = onThumb = onThumb.Clone();
            if (scale.IsFrozen) root.RenderTransform = scale = scale.Clone();

            _parts = new Parts(offThumb, onThumb, offTrack, onTrack, scale);
            SnapToState(_parts);
        }

        private void ReleaseParts()
        {
            if (_parts is not { } parts) return;
            _parts = null;
            SnapToState(parts);
            // 解除本地透明度覆盖，恢复模板持有的静态状态样式。
            parts.OffTrack.ClearValue(UIElement.OpacityProperty);
            parts.OnTrack.ClearValue(UIElement.OpacityProperty);
        }

        private void OnMotionEnabledChanged(object? sender, EventArgs e)
        {
            if (!MotionHelper.Enabled && _parts is { } parts) SnapToState(parts);
        }

        private void SnapToState(Parts parts)
        {
            ++_checkedRevision;
            var isChecked = IsChecked;
            Settle(parts.OffThumb, TranslateTransform.XProperty, isChecked ? 9 : -9);
            Settle(parts.OnThumb, TranslateTransform.XProperty, isChecked ? 9 : -9);
            Settle(parts.OffTrack, UIElement.OpacityProperty, isChecked ? 0 : 1);
            Settle(parts.OnTrack, UIElement.OpacityProperty, isChecked ? 1 : 0);
            Settle(parts.Scale, ScaleTransform.ScaleXProperty, 1);
            Settle(parts.Scale, ScaleTransform.ScaleYProperty, 1);
        }

        private void OnCheckedChanged(object? sender, EventArgs e)
        {
            if (_parts is not { } parts) return;
            if (!MotionHelper.Enabled)
            {
                SnapToState(parts);
                return;
            }

            var revision = ++_checkedRevision;
            var isChecked = IsChecked;
            var targetX = isChecked ? 9 : -9;
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            AnimateChecked(parts.OffThumb, TranslateTransform.XProperty, targetX, MotionSpeed.Fast, revision, ease);
            AnimateChecked(parts.OnThumb, TranslateTransform.XProperty, targetX, MotionSpeed.Fast, revision, ease);
            AnimateChecked(parts.OffTrack, UIElement.OpacityProperty, isChecked ? 0 : 1, MotionSpeed.Standard, revision);
            AnimateChecked(parts.OnTrack, UIElement.OpacityProperty, isChecked ? 1 : 0, MotionSpeed.Standard, revision);
        }

        private void AnimateChecked(DependencyObject target, DependencyProperty property, double to,
            MotionSpeed speed, int revision, IEasingFunction? ease = null)
        {
            // 先读呈现值，再替换动画；反向切换不会从旧基值重新起步。
            var from = (double)target.GetValue(property);
            if (from == to)
            {
                Settle(target, property, to);
                return;
            }

            var animation = new DoubleAnimation(from, to, MotionHelper.T(speed), FillBehavior.HoldEnd)
            {
                EasingFunction = ease
            };
            animation.Completed += (_, _) =>
            {
                if (revision == _checkedRevision) Settle(target, property, to);
            };
            BeginAnimation(target, property, animation);
        }

        private void OnPressedChanged(object? sender, EventArgs e)
        {
            if (_parts is not { } parts) return;
            if (!MotionHelper.Enabled)
            {
                Settle(parts.Scale, ScaleTransform.ScaleXProperty, 1);
                Settle(parts.Scale, ScaleTransform.ScaleYProperty, 1);
                return;
            }

            var to = IsPressed ? 0.97 : 1;
            AnimateScale(parts.Scale, ScaleTransform.ScaleXProperty, to);
            AnimateScale(parts.Scale, ScaleTransform.ScaleYProperty, to);
        }

        private static void AnimateScale(ScaleTransform target, DependencyProperty property, double to)
        {
            var from = (double)target.GetValue(property);
            target.SetValue(property, to);
            target.BeginAnimation(property, new DoubleAnimation(from, to, MotionHelper.T(MotionSpeed.Press), FillBehavior.Stop)
            {
                EasingFunction = new BackEase { Amplitude = 0.12, EasingMode = EasingMode.EaseOut }
            }, HandoffBehavior.SnapshotAndReplace);
        }

        private static void Settle(DependencyObject target, DependencyProperty property, double to)
        {
            BeginAnimation(target, property, null);
            // 本地基值覆盖模板触发器，防止下一次切换在取呈现值前跳到新终态。
            target.SetValue(property, to);
        }

        private static void BeginAnimation(DependencyObject target, DependencyProperty property, AnimationTimeline? animation)
        {
            if (target is Animatable transform)
                transform.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
            else if (target is UIElement element)
                element.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
