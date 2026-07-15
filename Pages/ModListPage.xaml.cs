using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UnturnedModManager.Models;
using Wpf.Ui.Controls;
using InfoBarSeverity = Wpf.Ui.Controls.InfoBarSeverity;

namespace UnturnedModManager.Pages;

public partial class ModListPage : Page
{
    public ObservableCollection<ModItem> Mods { get; } = new();

    public ModListPage()
    {
        InitializeComponent();
        ModListView.ItemsSource = Mods;
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => RefreshModList();

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshModList();
    private void OpenPluginsFolder_Click(object sender, RoutedEventArgs e)
    {
        var pluginsPath = GetPluginsPath();
        if (pluginsPath != null && Directory.Exists(pluginsPath))
            System.Diagnostics.Process.Start("explorer.exe", pluginsPath);
    }

    private void ModToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.ToggleSwitch toggle && toggle.Tag is string fileName)
        {
            var pluginsPath = GetPluginsPath();
            if (pluginsPath == null) return;

            bool enable = toggle.IsChecked == true;
            var dllPath = Path.Combine(pluginsPath, fileName);
            var disabledPath = dllPath + ".disabled";

            try
            {
                if (enable && File.Exists(disabledPath))
                    File.Move(disabledPath, dllPath);
                else if (!enable && File.Exists(dllPath))
                    File.Move(dllPath, disabledPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mod toggle error: {ex.Message}");
            }
        }
    }

    public void RefreshModList()
    {
        Mods.Clear();
        var pluginsPath = GetPluginsPath();
        if (pluginsPath == null || !Directory.Exists(pluginsPath))
        {
            EmptyLabel.Visibility = Visibility.Visible;
            return;
        }

        var modFiles = Directory.GetFiles(pluginsPath, "*.dll")
            .Concat(Directory.GetFiles(pluginsPath, "*.dll.disabled"))
            .Where(f => !f.EndsWith(".disabled") || !File.Exists(f.Replace(".disabled", "")))
            .ToList();

        foreach (var file in modFiles)
        {
            var fileName = Path.GetFileName(file);
            bool isEnabled = !fileName.EndsWith(".disabled");
            var lastWrite = File.GetLastWriteTime(file);
            Mods.Add(new ModItem
            {
                Name = Path.GetFileNameWithoutExtension(fileName.Replace(".disabled", "")),
                FileName = isEnabled ? fileName : fileName.Replace(".disabled", ""),
                IsEnabled = isEnabled,
                InstallTime = $"安装时间: {lastWrite:yyyy-MM-dd HH:mm:ss}"
            });
        }

        EmptyLabel.Visibility = Mods.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ModCountLabel.Text = $"共 {Mods.Count} 个模组";
    }

    private string? GetPluginsPath()
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (string.IsNullOrEmpty(gamePath)) return null;
        return Path.Combine(gamePath, "BepInEx", "plugins");
    }

    /// <summary>
    /// 拖拽悬浮事件：仅接受文件类型，显示 Copy 复制指针（带 + 号）。
    /// </summary>
    private void Page_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>
    /// 拖拽放下事件：提取 .dll / .dll.disabled 文件，强制覆盖复制到 BepInEx/plugins/，
    /// 完成后静默刷新列表并展示导入统计。
    /// </summary>
    private void Page_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Handled = true;
            return;
        }

        var paths = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
        e.Handled = true;

        if (paths == null || paths.Length == 0)
            return;

        var pluginsPath = GetPluginsPath();
        if (pluginsPath == null || !Directory.Exists(pluginsPath))
        {
            ShowStatus("请先在设置中配置有效的 Unturned 安装路径并安装 BepInEx", InfoBarSeverity.Warning);
            return;
        }

        Directory.CreateDirectory(pluginsPath);
        int count = 0;
        int skipped = 0;

        foreach (var path in paths)
        {
            // 同时接受 .dll 与 .dll.disabled（忽略大小写）
            var ext = Path.GetExtension(path);
            bool isDll = ext.Equals(".dll", StringComparison.OrdinalIgnoreCase);
            bool isDisabled = ext.Equals(".disabled", StringComparison.OrdinalIgnoreCase);
            if (!isDll && !isDisabled)
            {
                skipped++;
                continue;
            }

            var fileName = Path.GetFileName(path);
            var targetPath = Path.Combine(pluginsPath, fileName);

            try
            {
                File.Copy(path, targetPath, overwrite: true);
                count++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DragDrop] 复制失败 {path}: {ex.Message}");
                skipped++;
            }
        }

        RefreshModList();

        if (count > 0)
        {
            ShowStatus($"成功一键导入并安装了 {count} 个模组插件！", InfoBarSeverity.Success);
        }
        else
        {
            ShowStatus("未识别到可导入的 .dll 模组文件，请拖入 .dll 或 .dll.disabled", InfoBarSeverity.Warning);
        }
    }

    /// <summary>
    /// 在页面顶部 StatusInfoBar 上展示反馈消息。
    /// </summary>
    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    /// <summary>
    /// 拦截鼠标滚轮事件并手动路由到 NavigationView 内容区的 ScrollViewer。
    /// 修复：ListView 内部 ScrollViewer 在隧道阶段吞噬滚轮事件，导致外层 NavigationView 无法滚动。
    /// PreviewMouseWheel 是隧道事件，在子控件处理前先到达此处。
    /// 从 Page 向上遍历视觉树，找到第一个 ScrollViewer（即 NavigationView 内容区的 ScrollViewer）。
    /// </summary>
    private void Panel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 从 Page 自身向上寻找最近的 ScrollViewer 祖先
        // 这样可以跳过 ListView 内部的 ScrollViewer（它是 Page 的后代，不是祖先）
        DependencyObject? current = this;
        while (current != null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }
        }
    }
}