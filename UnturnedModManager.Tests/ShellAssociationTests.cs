using UnturnedModManager.Services;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class ShellAssociationTests
{
    [Theory]
    [InlineData("C:\\Games\\UMM\\UnturnedModManager.exe", "\"C:\\Games\\UMM\\UnturnedModManager.exe\" \"%1\"")]
    [InlineData("D:\\UnturnedModManager.exe", "\"D:\\UnturnedModManager.exe\" \"%1\"")]
    public void BuildOpenCommandLine_QuotesExecutableAndFirstArgument(string exePath, string expected)
    {
        var command = ShellAssociationService.BuildOpenCommandLine(exePath);
        Assert.Equal(expected, command);
    }

    [Fact]
    public void ShouldSkipRegistration_ReturnsTrue_WhenNotWindows()
    {
        var skip = ShellAssociationService.ShouldSkipRegistration(
            isWindows: false,
            isIsolated: false,
            executablePath: "C:\\UMM\\UnturnedModManager.exe");

        Assert.True(skip);
    }

    [Fact]
    public void ShouldSkipRegistration_ReturnsTrue_WhenIsolatedProfile()
    {
        var skip = ShellAssociationService.ShouldSkipRegistration(
            isWindows: true,
            isIsolated: true,
            executablePath: "C:\\UMM\\UnturnedModManager.exe");

        Assert.True(skip);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("C:\\Program Files\\dotnet\\dotnet.exe")]
    [InlineData("dotnet")]
    public void ShouldSkipRegistration_ReturnsTrue_WhenInvalidOrDotnetHost(string? exePath)
    {
        var skip = ShellAssociationService.ShouldSkipRegistration(
            isWindows: true,
            isIsolated: false,
            executablePath: exePath);

        Assert.True(skip);
    }

    [Fact]
    public void ShouldSkipRegistration_ReturnsFalse_WhenStandardWindowsExecutable()
    {
        var skip = ShellAssociationService.ShouldSkipRegistration(
            isWindows: true,
            isIsolated: false,
            executablePath: "C:\\Program Files\\UMM\\UnturnedModManager.exe");

        Assert.False(skip);
    }
}
