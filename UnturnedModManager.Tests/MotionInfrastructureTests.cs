using System.Windows;
using System.Windows.Media;
using UnturnedModManager.Controls.Motion;
using MotionHelper = UnturnedModManager.Helpers.Motion;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class MotionInfrastructureTests
{
    [Fact]
    public void IndicatorProperties_AreRegisteredWithShellDefaults()
    {
        AssertProperty(SegmentedIndicator.IsEnabledProperty, typeof(SegmentedIndicator), "IsEnabled", typeof(bool), false);
        AssertProperty(SegmentedIndicator.PillBrushProperty, typeof(SegmentedIndicator), "PillBrush", typeof(Brush), null);
        AssertProperty(SegmentedIndicator.PillBorderBrushProperty, typeof(SegmentedIndicator), "PillBorderBrush", typeof(Brush), null);
        AssertProperty(SegmentedIndicator.PillBorderThicknessProperty, typeof(SegmentedIndicator), "PillBorderThickness", typeof(Thickness), new Thickness(0));
        AssertProperty(SegmentedIndicator.PillCornerRadiusProperty, typeof(SegmentedIndicator), "PillCornerRadius", typeof(CornerRadius), new CornerRadius(14));
    }

    [Fact]
    public void DialogEnter_IsRegisteredAndDisabledByDefault() =>
        AssertProperty(DialogEnter.IsEnabledProperty, typeof(DialogEnter), "IsEnabled", typeof(bool), false);

    [Fact]
    public void BindableState_MatchesSystemAndCodeGate()
    {
        Assert.Equal(SystemParameters.ClientAreaAnimation, MotionHelper.State.Enabled);
        Assert.Equal(MotionHelper.State.Enabled, MotionHelper.Enabled);
    }

    private static void AssertProperty(DependencyProperty property, Type owner, string name, Type type, object? defaultValue)
    {
        Assert.Equal(owner, property.OwnerType);
        Assert.Equal(name, property.Name);
        Assert.Equal(type, property.PropertyType);
        Assert.Equal(defaultValue, property.DefaultMetadata.DefaultValue);
    }
}
