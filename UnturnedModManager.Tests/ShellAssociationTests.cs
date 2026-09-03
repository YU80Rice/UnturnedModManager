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

    [Fact]
    public void TryParseFileIntent_RecognizesExistingModPackage()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.ummpk");
        File.WriteAllText(tempFile, "dummy");
        try
        {
            var parsed = ShellAssociationService.TryParseFileIntent(tempFile, out var intent);
            Assert.True(parsed);
            Assert.NotNull(intent);
            Assert.Equal(ShellFileIntentType.ModPackage, intent!.Type);
            Assert.Equal(Path.GetFullPath(tempFile), intent.FilePath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryParseFileIntent_RecognizesExistingThemePackage()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.ummtheme");
        File.WriteAllText(tempFile, "dummy");
        try
        {
            var parsed = ShellAssociationService.TryParseFileIntent(tempFile, out var intent);
            Assert.True(parsed);
            Assert.NotNull(intent);
            Assert.Equal(ShellFileIntentType.ThemePackage, intent!.Type);
            Assert.Equal(Path.GetFullPath(tempFile), intent.FilePath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TryParseFileIntent_RejectsNonExistentFile()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.ummpk");
        var parsed = ShellAssociationService.TryParseFileIntent(nonExistent, out var intent);
        Assert.False(parsed);
        Assert.Null(intent);
    }

    [Fact]
    public void TryParseFileIntent_RejectsUnrelatedExtension()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.zip");
        File.WriteAllText(tempFile, "dummy");
        try
        {
            var parsed = ShellAssociationService.TryParseFileIntent(tempFile, out var intent);
            Assert.False(parsed);
            Assert.Null(intent);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void FindFileIntent_ExtractsFromMixedArguments()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.ummpk");
        File.WriteAllText(tempFile, "dummy");
        try
        {
            var args = new[] { "--flag", "something", $"\"{tempFile}\"", "extra" };
            var intent = ShellAssociationService.FindFileIntent(args);

            Assert.NotNull(intent);
            Assert.Equal(ShellFileIntentType.ModPackage, intent!.Type);
            Assert.Equal(Path.GetFullPath(tempFile), intent.FilePath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ShellIntentQueue_MaintainsFifoOrderAndEnforcesSingleModalGuard()
    {
        var queue = new ShellIntentQueue();
        var item1 = new ShellFileIntent("C:\\mod1.ummpk", ShellFileIntentType.ModPackage);
        var item2 = new ShellFileIntent("C:\\mod2.ummpk", ShellFileIntentType.ModPackage);

        queue.Enqueue(item1);
        queue.Enqueue(item2);

        Assert.Equal(2, queue.Count);
        Assert.False(queue.IsProcessing);

        // First item starts processing
        var dequeued1 = queue.TryStartNext(out var next1);
        Assert.True(dequeued1);
        Assert.Equal(item1, next1);
        Assert.True(queue.IsProcessing);
        Assert.Equal(1, queue.Count);

        // Guard: while processing, cannot start another
        var dequeued2 = queue.TryStartNext(out var next2);
        Assert.False(dequeued2);
        Assert.Null(next2);

        // Finish first item
        queue.FinishCurrent();
        Assert.False(queue.IsProcessing);

        // Now second item can start
        var dequeued3 = queue.TryStartNext(out var next3);
        Assert.True(dequeued3);
        Assert.Equal(item2, next3);
        Assert.True(queue.IsProcessing);
        Assert.Equal(0, queue.Count);

        // Finish second item
        queue.FinishCurrent();
        Assert.False(queue.IsProcessing);

        // When empty, returns false
        Assert.False(queue.TryStartNext(out _));
    }
}
