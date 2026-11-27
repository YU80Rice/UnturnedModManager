using System.Windows;
using UnturnedModManager.Controls.Motion;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class ToggleSwitchMotionTests
{
    [Fact]
    public void IsEnabled_IsRegisteredAndDisabledByDefault()
    {
        var property = ToggleSwitchMotion.IsEnabledProperty;
        Assert.Equal(typeof(ToggleSwitchMotion), property.OwnerType);
        Assert.Equal("IsEnabled", property.Name);
        Assert.Equal(typeof(bool), property.PropertyType);
        Assert.Equal(false, property.DefaultMetadata.DefaultValue);
    }

    [Fact]
    public void IsEnabled_RoundTripsOnNonToggleSwitchTarget()
    {
        var element = new DependencyObject();

        Assert.False(ToggleSwitchMotion.GetIsEnabled(element));
        ToggleSwitchMotion.SetIsEnabled(element, true);
        Assert.True(ToggleSwitchMotion.GetIsEnabled(element));
        ToggleSwitchMotion.SetIsEnabled(element, false);
        Assert.False(ToggleSwitchMotion.GetIsEnabled(element));
    }

    [Fact]
    public void IsEnabled_NullTargetIsIgnored()
    {
        ToggleSwitchMotion.SetIsEnabled(null, true);
        Assert.False(ToggleSwitchMotion.GetIsEnabled(null));
    }
}
