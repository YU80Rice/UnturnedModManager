using System.Windows;
using UnturnedModManager.Helpers;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class MotionTests
{
    [Theory]
    [InlineData(MotionSpeed.Press, 90)]
    [InlineData(MotionSpeed.Fast, 140)]
    [InlineData(MotionSpeed.Standard, 180)]
    [InlineData(MotionSpeed.Enter, 200)]
    [InlineData(MotionSpeed.Dialog, 220)]
    [InlineData(MotionSpeed.Exit, 120)]
    public void TimingTokens_MatchContract(MotionSpeed speed, int milliseconds) =>
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), Motion.T(speed));

    [Fact]
    public void Enabled_ReflectsSystemClientAreaAnimation() =>
        Assert.Equal(SystemParameters.ClientAreaAnimation, Motion.Enabled);
}
