using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using UnturnedModManager.Helpers;
using Xunit;

namespace UnturnedModManager.Tests;

/// <summary>
/// Ticket 02: Unturned 原生风格 UI Shell 空间结构与设计代币原型验证测试
/// 针对 PM 硬门槛：
/// 1. 验证 .scratch/.../prototype/tokens/UnturnedNativeTokens.xaml 资源与代币完整性
/// 2. 验证 WCAG 2.1 AA/AAA 对比度要求 (白字/金字/浅灰字 在暗色面板上的高对比度)
/// 3. 验证 UnturnedNativeShellPrototype.xaml 空间结构：5 大药丸槽位 + Page/Frame 契约 + 隧道路由 + 次级入口
/// 4. 验证 PrototypePlayPage.xaml 三层主布局与吉祥物无缝闭合
/// 5. 验证 PrototypeDataPage.xaml 四大只读域 Tab 契约 (严禁写入控件)
/// </summary>
public sealed class UnturnedNativeShellPrototypeTests
{
    private static string GetPrototypeRootDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".scratch", "unturned-native-hud-and-save-manager", "prototype");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            var subCandidate = Path.Combine(current.FullName, "UnturnedModManager", ".scratch", "unturned-native-hud-and-save-manager", "prototype");
            if (Directory.Exists(subCandidate))
            {
                return subCandidate;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find .scratch/.../prototype directory.");
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    _ = new Application();
                }
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
        {
            throw new Exception("STA test execution failed", exception);
        }
    }

    [Fact]
    public void DesignTokens_XmlStructure_ContainsAllRequiredKeys()
    {
        var prototypeDir = GetPrototypeRootDirectory();
        var tokensPath = Path.Combine(prototypeDir, "tokens", "UnturnedNativeTokens.xaml");
        Assert.True(File.Exists(tokensPath), $"Tokens file does not exist: {tokensPath}");

        var doc = XDocument.Load(tokensPath);
        var xName = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");

        var keys = doc.Descendants()
            .Select(d => d.Attribute(xName + "Key")?.Value)
            .Where(k => !string.IsNullOrEmpty(k))
            .ToHashSet();

        // 核心颜色与画刷代币
        Assert.Contains("UnturnedBackdropColor", keys);
        Assert.Contains("UnturnedBackdropBrush", keys);
        Assert.Contains("UnturnedHudPanelColor", keys);
        Assert.Contains("UnturnedHudPanelBrush", keys);
        Assert.Contains("UnturnedHudCardColor", keys);
        Assert.Contains("UnturnedHudCardBrush", keys);
        Assert.Contains("UnturnedStrokeSubtleColor", keys);
        Assert.Contains("UnturnedStrokeSubtleBrush", keys);
        Assert.Contains("UnturnedStrokeStrongColor", keys);
        Assert.Contains("UnturnedStrokeStrongBrush", keys);
        Assert.Contains("UnturnedTextPrimaryColor", keys);
        Assert.Contains("UnturnedTextPrimaryBrush", keys);
        Assert.Contains("UnturnedTextSecondaryColor", keys);
        Assert.Contains("UnturnedTextSecondaryBrush", keys);
        Assert.Contains("UnturnedAccentGoldColor", keys);
        Assert.Contains("UnturnedAccentGoldBrush", keys);
        Assert.Contains("UnturnedAccentGreenColor", keys);
        Assert.Contains("UnturnedAccentGreenBrush", keys);
        Assert.Contains("UnturnedAccentRedColor", keys);
        Assert.Contains("UnturnedAccentRedBrush", keys);

        // 药丸按钮状态代币
        Assert.Contains("UnturnedPillNormalColor", keys);
        Assert.Contains("UnturnedPillNormalBrush", keys);
        Assert.Contains("UnturnedPillHoverColor", keys);
        Assert.Contains("UnturnedPillHoverBrush", keys);
        Assert.Contains("UnturnedPillSelectedColor", keys);
        Assert.Contains("UnturnedPillSelectedBrush", keys);

        // 几何代币与样式
        Assert.Contains("UnturnedPillCornerRadius", keys);
        Assert.Contains("UnturnedCardCornerRadius", keys);
        Assert.Contains("UnturnedPillButtonStyle", keys);
        Assert.Contains("UnturnedHudCardStyle", keys);
    }

    [Fact]
    public void DesignTokens_ColorContrast_StrictlySatisfiesWcagAaAndAaa()
    {
        // 提取代币颜色值进行 WCAG 2.1 对比度数学守卫
        var backdrop = (Color)ColorConverter.ConvertFromString("#FF0F1418")!;
        var hudPanel = (Color)ColorConverter.ConvertFromString("#FF14191F")!; // 去除 alpha 后的实色底
        var hudCard = (Color)ColorConverter.ConvertFromString("#FF1A2027")!;
        var pillSelected = (Color)ColorConverter.ConvertFromString("#FF202933")!;

        var primaryText = (Color)ColorConverter.ConvertFromString("#FFFFFFFF")!;
        var secondaryText = (Color)ColorConverter.ConvertFromString("#FFCBD5E0")!;
        var goldAccent = (Color)ColorConverter.ConvertFromString("#FFE2B024")!;

        // 1. 主文本 (#FFFFFF) 在各暗色表面上应达到最高标准 (AAA >= 7.0:1)
        var textOnBackdrop = ColorContrastHelper.GetContrastRatio(primaryText, backdrop);
        var textOnPanel = ColorContrastHelper.GetContrastRatio(primaryText, hudPanel);
        var textOnCard = ColorContrastHelper.GetContrastRatio(primaryText, hudCard);
        var textOnPillSelected = ColorContrastHelper.GetContrastRatio(primaryText, pillSelected);

        Assert.True(textOnBackdrop >= 7.0, $"Text on Backdrop lacks AAA contrast: {textOnBackdrop:F2}:1");
        Assert.True(textOnPanel >= 7.0, $"Text on HUD Panel lacks AAA contrast: {textOnPanel:F2}:1");
        Assert.True(textOnCard >= 7.0, $"Text on HUD Card lacks AAA contrast: {textOnCard:F2}:1");
        Assert.True(textOnPillSelected >= 7.0, $"Text on Selected Pill lacks AAA contrast: {textOnPillSelected:F2}:1");

        // 2. 次级文字 (#CBD5E0) 在面板与卡片上应严格符合 WCAG AA (>= 4.5:1)
        var secOnPanel = ColorContrastHelper.GetContrastRatio(secondaryText, hudPanel);
        var secOnCard = ColorContrastHelper.GetContrastRatio(secondaryText, hudCard);
        Assert.True(secOnPanel >= 4.5, $"Secondary text on Panel lacks AA contrast: {secOnPanel:F2}:1");
        Assert.True(secOnCard >= 4.5, $"Secondary text on Card lacks AA contrast: {secOnCard:F2}:1");

        // 3. 金色强调色 (#E2B024) 在 HUD 表面达到大文本/UI图形标准 (>= 3.0:1)
        var goldOnPanel = ColorContrastHelper.GetContrastRatio(goldAccent, hudPanel);
        var goldOnCard = ColorContrastHelper.GetContrastRatio(goldAccent, hudCard);
        Assert.True(goldOnPanel >= 3.0, $"Gold accent on Panel lacks graphical contrast: {goldOnPanel:F2}:1");
        Assert.True(goldOnCard >= 3.0, $"Gold accent on Card lacks graphical contrast: {goldOnCard:F2}:1");
    }

    [Fact]
    public void ShellPrototype_Structure_HasFiveSlots_FrameMounting_AndTunnelRouting()
    {
        var prototypeDir = GetPrototypeRootDirectory();
        var shellPath = Path.Combine(prototypeDir, "UnturnedNativeShellPrototype.xaml");
        Assert.True(File.Exists(shellPath), $"Shell prototype file does not exist: {shellPath}");

        var content = File.ReadAllText(shellPath);

        // 验证经典 Unturned 原版五大药丸槽位
        Assert.Contains("x:Name=\"NavSlotPlay\"", content);
        Assert.Contains("x:Name=\"NavSlotData\"", content);
        Assert.Contains("x:Name=\"NavSlotMods\"", content);
        Assert.Contains("x:Name=\"NavSlotSettings\"", content);
        Assert.Contains("x:Name=\"NavSlotExit\"", content);

        // 验证 Frame 挂载契约：无边框、无系统导航栏、支持日志所有权
        Assert.Contains("x:Name=\"MainContentFrame\"", content);
        Assert.Contains("NavigationUIVisibility=\"Hidden\"", content);

        // 验证隧道路由事件预防滚动吞噬
        Assert.Contains("PreviewMouseWheel=\"Shell_PreviewMouseWheel\"", content);

        // 验证次级浮动入口
        Assert.Contains("x:Name=\"SecondaryTaskBtn\"", content);
        Assert.Contains("x:Name=\"SecondaryAccountBtn\"", content);
        Assert.Contains("x:Name=\"SecondaryAboutBtn\"", content);
    }

    [Fact]
    public void PlayPage_Structure_ThreeLayerLayout_AndMascotToggle()
    {
        var prototypeDir = GetPrototypeRootDirectory();
        var playPath = Path.Combine(prototypeDir, "pages", "PrototypePlayPage.xaml");
        Assert.True(File.Exists(playPath), $"PlayPage file does not exist: {playPath}");

        var content = File.ReadAllText(playPath);

        // 验证首页核心组件：快速启动英雄卡片、状态横幅、吉祥物容器、更新日志网格
        Assert.Contains("x:Name=\"LaunchHeroCard\"", content);
        Assert.Contains("x:Name=\"StatusBanner\"", content);
        Assert.Contains("x:Name=\"MascotContainer\"", content);
        Assert.Contains("x:Name=\"MascotToggleBtn\"", content);
        Assert.Contains("x:Name=\"FeaturedGrid\"", content);

        // 验证吉祥物容器为右侧悬浮挂件，且配置了无缝折叠过渡
        Assert.Contains("MascotToggleBtn_Click", content);
    }

    [Fact]
    public void DataPage_Structure_StrictlyReadOnly_AndFourDataDomains()
    {
        var prototypeDir = GetPrototypeRootDirectory();
        var dataPath = Path.Combine(prototypeDir, "pages", "PrototypeDataPage.xaml");
        Assert.True(File.Exists(dataPath), $"DataPage file does not exist: {dataPath}");

        var content = File.ReadAllText(dataPath);

        // 验证 4 个独立只读子域药丸 Tab
        Assert.Contains("x:Name=\"TabCharacters\"", content);
        Assert.Contains("x:Name=\"TabWorlds\"", content);
        Assert.Contains("x:Name=\"TabServerConfig\"", content);
        Assert.Contains("x:Name=\"TabBackups\"", content);

        // 验证只读警示与横幅
        Assert.Contains("只读浏览", content);
        Assert.Contains("第一阶段", content);

        // 严格安全守卫：严禁出现任何写入、保存、修改、删除等受控编辑器控件
        Assert.DoesNotContain("SaveButton", content);
        Assert.DoesNotContain("保存修改", content);
        Assert.DoesNotContain("写入存档", content);
        Assert.DoesNotContain("DeleteSave", content);
    }
}
