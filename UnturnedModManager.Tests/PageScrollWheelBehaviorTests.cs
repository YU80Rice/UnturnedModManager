using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UnturnedModManager.Helpers;
using UnturnedModManager.Pages;
using Xunit;

namespace UnturnedModManager.Tests;

public sealed class PageScrollWheelBehaviorTests
{
    [Theory]
    [InlineData("Pages/HomePage.xaml")]
    [InlineData("Pages/SettingsPage.xaml")]
    [InlineData("Pages/AboutPage.xaml")]
    [InlineData("Pages/ModListPage.xaml")]
    [InlineData("Pages/CommunityPage.xaml")]
    [InlineData("Pages/CommunityDetailPage.xaml")]
    [InlineData("Pages/TaskCenterPage.xaml")]
    public void Page_WithScrollViewer_MustWirePreviewMouseWheel(string relativePagePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        string? targetFile = null;
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePagePath);
            if (File.Exists(candidate)) { targetFile = candidate; break; }
            var subCandidate = Path.Combine(current.FullName, "UnturnedModManager", relativePagePath);
            if (File.Exists(subCandidate)) { targetFile = subCandidate; break; }
            current = current.Parent;
        }

        Assert.NotNull(targetFile);
        Assert.True(File.Exists(targetFile), $"Page file not found: {relativePagePath}");
        var content = File.ReadAllText(targetFile);

        // All scrollable pages must hook PreviewMouseWheel to route tunneling wheel events
        Assert.Contains("PreviewMouseWheel=", content);
    }
}
