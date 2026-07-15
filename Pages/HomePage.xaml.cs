using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UnturnedModManager.Helpers;
using UnturnedModManager.Models;
using Wpf.Ui.Controls;

namespace UnturnedModManager.Pages;

public partial class HomePage : Page
{
    private const string WinHttpDll = "winhttp.dll";
    private const string WinHttpDisabled = "winhttp.dll.disabled";
    private const string BepInExCoreRelativePath = @"BepInEx\core\BepInEx.dll";
    private const string DxvkD3d11Dll = "d3d11.dll";
    private const string DxvkDxgiDll = "dxgi.dll";
    private const string DxvkD3d11Disabled = "d3d11.dll.disabled";
    private const string DxvkDxgiDisabled = "dxgi.dll.disabled";

    /// <summary>
    /// Unturned 游戏主程序文件名（不含反作弊）。模组模式启动此 EXE 并附 -NoBattlEye 绕过 BE。
    /// </summary>
    private const string UnturnedExeName = "Unturned.exe";

    /// <summary>
    /// Unturned 反作弊壳程序文件名。原版模式启动此 EXE 以原生加载 BattlEye + Steam 覆盖层。
    /// </summary>
    private const string UnturnedBEExeName = "Unturned_BE.exe";

    /// <summary>
    /// BepInEx 下载源列表。优先使用国内 ghproxy 镜像，失败时回退到官方 GitHub。
    /// </summary>
    private static readonly string[] BepInExDownloadUrls =
    {
        "https://mirror.ghproxy.com/https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip",
        "https://github.com/BepInEx/BepInEx/releases/download/v5.4.22/BepInEx_x64_5.4.22.0.zip"
    };

    /// <summary>
    /// DXVK 下载源列表。优先国内 GHProxy 中继，失败时回退到官方 GitHub。
    /// 仅需从 tar.gz 中提取 x64/d3d11.dll 和 x64/dxgi.dll 部署到游戏根目录。
    /// </summary>
    private static readonly string[] DxvkDownloadUrls =
    {
        "https://mirror.ghproxy.com/https://github.com/doitsujin/dxvk/releases/download/v2.4/dxvk-2.4.tar.gz",
        "https://github.com/doitsujin/dxvk/releases/download/v2.4/dxvk-2.4.tar.gz"
    };

    private bool _suppressToggleHandler;

    /// <summary>
    /// 标记本次会话是否已完成首次启动路径探测，避免重复弹窗打扰用户。
    /// </summary>
    private bool _firstLaunchDetectionDone;

    /// <summary>
    /// 状态提示条倒计时计时器。每 100ms 触发一次，10 秒后自动隐藏 InfoBar。
    /// </summary>
    private readonly DispatcherTimer _statusTimer;

    /// <summary>
    /// 状态提示条倒计时总时长（毫秒）。
    /// </summary>
    private const int StatusDurationMs = 10000;

    /// <summary>
    /// 倒计时计时器间隔（毫秒）。每次触发进度条递减 1%。
    /// </summary>
    private const int StatusTickIntervalMs = 100;

    public HomePage()
    {
        InitializeComponent();
        Loaded += HomePage_Loaded;

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(StatusTickIntervalMs)
        };
        _statusTimer.Tick += OnStatusTimerTick;
    }

    /// <summary>
    /// 倒计时 Tick：进度条从 100 递减到 0，到 0 时隐藏 InfoBar。
    /// </summary>
    private void OnStatusTimerTick(object? sender, EventArgs e)
    {
        StatusProgressBar.Value = Math.Max(0, StatusProgressBar.Value - 1);

        if (StatusProgressBar.Value <= 0)
        {
            _statusTimer.Stop();
            StatusInfoBar.IsOpen = false;
            StatusContainer.Visibility = Visibility.Collapsed;
        }
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshGlobalModStatus();
        RefreshBepInExStatus();
        RefreshDxvkToggle();
        RefreshGpuInfo();
        RefreshCrashAlert();

        if (_firstLaunchDetectionDone) return;
        _firstLaunchDetectionDone = true;

        await TryFirstLaunchDetectionAsync();
    }

    /// <summary>
    /// 根据 AppSettings.EnableDxvk 与游戏根目录 DLL 实际存在性同步开关状态。
    /// </summary>
    private void RefreshDxvkToggle()
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        bool dllsPresent = !string.IsNullOrEmpty(gamePath)
            && File.Exists(Path.Combine(gamePath, DxvkD3d11Dll))
            && File.Exists(Path.Combine(gamePath, DxvkDxgiDll));

        _suppressToggleHandler = true;
        // 开关状态以 DLL 是否实际存在为准（更贴近运行时真实状态）
        DxvkOptimizerToggle.IsChecked = dllsPresent;
        _suppressToggleHandler = false;
    }

    /// <summary>
    /// v1.6.8 新增：检测主显卡，在 DXVK 开关下方显示 GPU 信息与 DXVK 推荐度。
    /// 首次启动若 GPU 检测结果为"不推荐"，且用户从未配置过 DXVK（EnableDxvk=false 且 DxvkRecommendedByGpu=null），
    /// 则写入推荐值并确保 DXVK 关闭，避免老架构显卡（如 GTX 1060 Pascal）启用 DXVK 后严重降帧。
    /// </summary>
    private void RefreshGpuInfo()
    {
        GpuInfo gpu;
        try
        {
            gpu = Task.Run(() => GpuDetector.DetectPrimary()).Result;
        }
        catch
        {
            GpuInfoBorder.Visibility = Visibility.Collapsed;
            return;
        }

        if (string.IsNullOrEmpty(gpu.Name))
        {
            GpuInfoBorder.Visibility = Visibility.Collapsed;
            return;
        }

        GpuNameText.Text = $"🖥️ {gpu.Name}  ·  {gpu.VendorName} {gpu.ArchitectureName}";
        GpuRecommendationText.Text = $"{gpu.RecommendationText}  —  {gpu.RecommendationDetail}";
        GpuInfoBorder.Visibility = Visibility.Visible;

        // 首次启动智能默认：GPU 不推荐 DXVK 时，写入推荐值并确保 DXVK 关闭
        if (AppSettings.DxvkRecommendedByGpu == null)
        {
            bool recommended = gpu.DxvkRecommendation != DxvkRecommendation.NotRecommended;
            AppSettings.DxvkRecommendedByGpu = recommended;

            if (!recommended && !AppSettings.EnableDxvk)
            {
                // 老架构显卡 + 用户从未启用过 DXVK -> 确保关闭
                _suppressToggleHandler = true;
                DxvkOptimizerToggle.IsChecked = false;
                _suppressToggleHandler = false;
            }
        }
    }

    /// <summary>
    /// 首次启动主动探测：当配置中 UnturnedInstallPath 为空时，后台扫描 Steam 注册表。
    /// 命中后弹出 ContentDialog 询问用户是否采用该路径。
    /// </summary>
    private async Task TryFirstLaunchDetectionAsync()
    {
        if (!string.IsNullOrEmpty(AppSettings.UnturnedInstallPath))
            return;

        var detectedPath = await Task.Run(AppSettings.DetectSteamUnturnedPath);
        if (string.IsNullOrEmpty(detectedPath))
            return;

        var confirmed = await WpfUiDialogHelper.ConfirmDetectedGamePathAsync(detectedPath);
        if (!confirmed)
            return;

        AppSettings.UnturnedInstallPath = detectedPath;
        RefreshGlobalModStatus();
        RefreshBepInExStatus();
        await WpfUiDialogHelper.ShowPathConfiguredSuccessAsync();
    }

    /// <summary>
    /// 根据配置中的 LastSessionCrashed 标志显示或隐藏崩溃提示条。
    /// </summary>
    private void RefreshCrashAlert()
    {
        CrashAlertBorder.Visibility = AppSettings.LastSessionCrashed
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RefreshBepInExStatus()
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        bool hasGamePath = !string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath);
        bool bepInExInstalled = hasGamePath && File.Exists(Path.Combine(gamePath, BepInExCoreRelativePath));

        if (bepInExInstalled)
        {
            BepInExStatusIndicator.Fill = (System.Windows.Media.Brush)FindResource("SystemFillColorSuccessBrush");
            BepInExStatusTitle.Text = "BepInEx 已就绪";
            BepInExStatusDetail.Text = $"检测到加载器：{BepInExCoreRelativePath}";
            InstallBepInExButton.Visibility = Visibility.Collapsed;
            RepairBepInExButton.Visibility = Visibility.Visible;
        }
        else
        {
            BepInExStatusIndicator.Fill = (System.Windows.Media.Brush)FindResource("SystemFillColorCriticalBrush");
            BepInExStatusTitle.Text = "BepInEx 未安装";
            BepInExStatusDetail.Text = hasGamePath
                ? "游戏路径有效，但未找到 BepInEx 加载器。"
                : "未配置有效的 Unturned 安装路径，无法检测模组环境。";
            InstallBepInExButton.Visibility = hasGamePath ? Visibility.Visible : Visibility.Collapsed;
            RepairBepInExButton.Visibility = Visibility.Collapsed;
        }

        // 启动游戏按钮始终保持可用，由点击事件内部做业务决策
        LaunchGameButton.IsEnabled = true;
    }

    private void RefreshGlobalModStatus()
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (string.IsNullOrEmpty(gamePath))
        {
            _suppressToggleHandler = true;
            GlobalModToggle.IsChecked = false;
            _suppressToggleHandler = false;
            return;
        }

        var dllPath = Path.Combine(gamePath, WinHttpDll);
        _suppressToggleHandler = true;
        GlobalModToggle.IsChecked = File.Exists(dllPath);
        _suppressToggleHandler = false;
    }

    private void GlobalModToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleHandler) return;
        if (sender is not ToggleSwitch toggle) return;

        var gamePath = AppSettings.UnturnedInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowStatus("请先在设置中配置有效的 Unturned 安装路径", InfoBarSeverity.Warning);
            _suppressToggleHandler = true;
            toggle.IsChecked = false;
            _suppressToggleHandler = false;
            return;
        }

        var dllPath = Path.Combine(gamePath, WinHttpDll);
        var disabledPath = Path.Combine(gamePath, WinHttpDisabled);

        bool wantEnable = toggle.IsChecked == true;

        try
        {
            if (wantEnable)
            {
                if (File.Exists(dllPath))
                {
                    ShowStatus("全局模组环境已启用", InfoBarSeverity.Success);
                }
                else if (File.Exists(disabledPath))
                {
                    SafeMoveFile(disabledPath, dllPath);
                    ShowStatus("已启用全局模组环境（winhttp.dll 已恢复）", InfoBarSeverity.Success);
                }
                else
                {
                    _suppressToggleHandler = true;
                    toggle.IsChecked = false;
                    _suppressToggleHandler = false;
                    ShowStatus("请先确保 BepInEx 的 winhttp.dll 已放入游戏根目录", InfoBarSeverity.Error);
                }
            }
            else
            {
                if (File.Exists(dllPath))
                {
                    SafeMoveFile(dllPath, disabledPath);
                    ShowStatus("已禁用全局模组环境（winhttp.dll 已停用）", InfoBarSeverity.Informational);
                }
                else if (File.Exists(disabledPath))
                {
                    ShowStatus("全局模组环境已禁用", InfoBarSeverity.Informational);
                }
                else
                {
                    ShowStatus("未找到 winhttp.dll，可能 BepInEx 未正确安装", InfoBarSeverity.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _suppressToggleHandler = true;
            toggle.IsChecked = !wantEnable;
            _suppressToggleHandler = false;
            ShowStatus($"切换失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            RefreshBepInExStatus();
        }
    }

    private async void LaunchGameButton_Click(object sender, RoutedEventArgs e)
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowStatus("请先在设置中配置有效的 Unturned 安装路径", InfoBarSeverity.Warning);
            return;
        }

        bool bepInExInstalled = File.Exists(Path.Combine(gamePath, BepInExCoreRelativePath));
        bool modsEnabled = GlobalModToggle.IsChecked == true;

        // 步骤 A：已安装 BepInEx，直接放行
        if (bepInExInstalled)
        {
            LaunchGame(gamePath, modsEnabled);
            return;
        }

        // 步骤 B：未安装 BepInEx
        // 分支 B1：全局模组开关关闭，直接启动原版
        if (!modsEnabled)
        {
            LaunchGame(gamePath, modsEnabled: false);
            return;
        }

        // 分支 B2：全局模组开关开启，询问是否安装
        bool wantInstall = await WpfUiDialogHelper.ConfirmInstallBeforeLaunchAsync();
        if (wantInstall)
        {
            // 中止启动，转去安装环境
            await RunBepInExDeploymentAsync(gamePath, "安装");
            return;
        }

        // 用户拒绝安装，二次确认是否以原版启动
        bool launchVanilla = await WpfUiDialogHelper.ConfirmLaunchVanillaAsync();
        if (!launchVanilla)
        {
            // 完全中止启动
            return;
        }

        // 暂时关闭全局环境开关，然后启动原版
        DisableGlobalModsTemporarily();
        LaunchGame(gamePath, modsEnabled: false);
    }

    private void LaunchGame(string gamePath, bool modsEnabled)
    {
        try
        {
            EnsureModFileState(gamePath, modsEnabled);

            // 修复 v1.6.5：已安装 BepInEx 的用户走"步骤 A"直放路径，此前会跳过 DeployEmbeddedCoreMods，
            // 导致 BepInEx/plugins/ 下没有 LaunchPerfOptimizer.dll 与 WaterPerfOptimizer.dll，
            // BepInEx 日志显示 "0 plugins to load" 且游戏闪退。模组模式下必须每次启动都重新释放核心模组（覆盖写）。
            if (modsEnabled)
            {
                DeployEmbeddedCoreMods(gamePath);
            }

            // 修复 v1.6.6：直放 Unturned.exe 时绕过 Steam 启动流程，Steam SDK 会因找不到 App ID 而无法注入
            // GameOverlayRenderer64.dll。无论模组 / 原版模式都无条件写入 steam_appid.txt，
            // 让 Steam SDK 在初始化时能正确识别本进程属于 App 304930。
            EnsureSteamAppIdFile(gamePath);

            RefreshGlobalModStatus();

            // DXVK 自适应优化：开启时动态生成 dxvk.conf + 注入 DXVK_HUD 环境变量
            bool dxvkEnabled = AppSettings.EnableDxvk;
            if (dxvkEnabled)
            {
                EnsureDxvkConfFile(gamePath);
            }

            string exePath;
            string arguments;
            string modeLabel;

            if (modsEnabled)
            {
                // 模组/P2P 联机模式：Unturned.exe -NoBattlEye，绕过 BE 弹窗并加载 BepInEx 模组环境
                exePath = Path.Combine(gamePath, UnturnedExeName);
                arguments = "-NoBattlEye";
                modeLabel = "已启用模组 · 跳过 BattlEye";
                if (!File.Exists(exePath))
                {
                    ShowStatus($"未找到游戏主程序：{exePath}", InfoBarSeverity.Error);
                    return;
                }
            }
            else
            {
                // 原版联机模式：Unturned_BE.exe（无参数），原生启动 BattlEye + Steam 覆盖层
                exePath = Path.Combine(gamePath, UnturnedBEExeName);
                arguments = string.Empty;
                modeLabel = "纯净模式 · 含 BattlEye 反作弊";
                if (!File.Exists(exePath))
                {
                    ShowStatus($"未找到反作弊壳程序：{exePath}", InfoBarSeverity.Error);
                    return;
                }
            }

            // UseShellExecute=false 以支持 EnvironmentVariables（DXVK_HUD / SteamAppId 注入需要）
            // 注意：启动器不应以管理员权限运行，否则 Windows 会拦截 Steam 覆盖层的注入
            var psi = new ProcessStartInfo(exePath, arguments)
            {
                UseShellExecute = false,
                WorkingDirectory = gamePath
            };

            // 修复 v1.6.6：注入 Steam Overlay 必需的环境变量。
            // 即使 Steam 客户端以普通用户身份运行，通过环境变量显式声明 App ID，
            // 可让 Steam SDK 在游戏进程初始化阶段就向 Steam 客户端注册 Overlay 注入意图，
            // 避免因直放路径（绕过 Steam 启动流程）导致 Overlay 失效。
            psi.EnvironmentVariables["SteamAppId"] = "304930";
            psi.EnvironmentVariables["SteamGameId"] = "304930";
            psi.EnvironmentVariables["SteamOverlayGameId"] = "304930";

            // 修复 v1.6.7：移除 DXVK_HUD 环境变量注入。
            // 此前 v1.6.6 在启用 DXVK 时注入了 DXVK_HUD=compiler,fps,api，导致游戏左上角
            // 持续显示编译器/FPS/API 调试 HUD，干扰玩家体验。环境变量优先级高于 dxvk.conf，
            // 用户即使手动改 dxvk.hud=False 也无法关闭。
            // 现在移除该注入，HUD 默认不显示。需要调试的开发者可自行在 dxvk.conf 中加：
            //   dxvk.hud = compiler,fps,api
            Process.Start(psi);

            var hudSuffix = dxvkEnabled ? " · DXVK 已启用" : string.Empty;
            ShowStatus($"正在启动游戏（{modeLabel}{hudSuffix}）...", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowStatus($"无法启动游戏：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    /// <summary>
    /// 确保游戏根目录下存在 steam_appid.txt 且内容为 "304930"。
    /// 直放 Unturned.exe 时绕过了 Steam 的正常启动流程，Steam SDK 在初始化时
    /// 会读取此文件以识别 App ID；若缺失，Steam Overlay、成就、P2P 联机等
    /// Steamworks 功能将全部失效，部分情况下还会触发 Steam 重新接管启动流程
    /// 并拉起 BattlEye 覆盖原版，破坏模组模式。
    /// </summary>
    private static void EnsureSteamAppIdFile(string gamePath)
    {
        var appidPath = Path.Combine(gamePath, "steam_appid.txt");
        const string expectedContent = "304930";

        try
        {
            if (File.Exists(appidPath))
            {
                var current = File.ReadAllText(appidPath).Trim();
                if (current == expectedContent) return;
            }
            File.WriteAllText(appidPath, expectedContent + "\n");
            System.Diagnostics.Debug.WriteLine($"[SteamAppId] 已写入: {appidPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SteamAppId] 写入失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据 CPU 物理线程数动态生成 dxvk.conf 到游戏根目录。
    /// 启用管线库以减少着色器编译卡顿，编译器线程数自适应 CPU。
    /// v1.6.6 追加三项安全鲁棒性配置，防止与 Steam/Discord Overlay 多重 Hook 冲突：
    /// - dxgi.deferSurfaceCreation：延迟 DXGI 表面创建，给 Steam Overlay 留出 Hook 前置时间
    /// - dxvk.allowFse：禁用全屏独占，避免独占全屏切换桌面时的图形上下文崩溃
    /// - dxvk.allowDialogMode：允许对话框模式，优化多屏 / 窗口化焦点的 Vulkan 适配
    /// </summary>
    private static void EnsureDxvkConfFile(string gamePath)
    {
        var confPath = Path.Combine(gamePath, "dxvk.conf");
        var processorCount = Environment.ProcessorCount;

        // 多保 1 个线程给游戏主循环，避免编译器线程抢尽 CPU；最少 2 线程
        int compilerThreads = Math.Max(2, processorCount - 1);

        var content = $"# 动态自适应线程设置（由 UnturnedModManager 自动生成）\n"
                    + $"# 检测到 CPU 物理线程数：{processorCount}\n"
                    + $"dxvk.numCompilerThreads = {compilerThreads}\n"
                    + $"\n"
                    + $"# 开启管线库以减少着色器卡顿\n"
                    + $"dxvk.enableGraphicsPipelineLibrary = True\n"
                    + $"\n"
                    + $"# v1.6.6 安全鲁棒性配置：防止与 Steam / Discord Overlay 多重 Hook 冲突\n"
                    + $"# 延迟 DXGI 表面创建，给 Steam Overlay 留出 Hook 前置时间\n"
                    + $"dxgi.deferSurfaceCreation = True\n"
                    + $"# 禁用全屏独占，避免独占全屏切换桌面时的图形上下文崩溃\n"
                    + $"dxvk.allowFse = False\n"
                    + $"# 允许对话框模式，优化多屏 / 窗口化焦点的 Vulkan 适配\n"
                    + $"dxvk.allowDialogMode = True\n";

        try
        {
            File.WriteAllText(confPath, content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DxvkConf] 写入失败: {ex.Message}");
        }
    }

    private void DisableGlobalModsTemporarily()
    {
        _suppressToggleHandler = true;
        GlobalModToggle.IsChecked = false;
        _suppressToggleHandler = false;
        RefreshGlobalModStatus();
    }

    private static void SafeMoveFile(string source, string destination)
    {
        if (!File.Exists(source)) return;
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }
        File.Move(source, destination, overwrite: true);
    }

    private void EnsureModFileState(string gamePath, bool enabled)
    {
        var dllPath = Path.Combine(gamePath, WinHttpDll);
        var disabledPath = Path.Combine(gamePath, WinHttpDisabled);

        if (enabled)
        {
            if (!File.Exists(dllPath) && File.Exists(disabledPath))
                SafeMoveFile(disabledPath, dllPath);
        }
        else
        {
            if (File.Exists(dllPath))
                SafeMoveFile(dllPath, disabledPath);
        }
    }

    /// <summary>
    /// DXVK 极速优化开关切换事件。
    /// ON  -> 优先复用已存在的 DLL；否则从 .disabled 备份恢复；都没有才下载部署。
    /// OFF -> 将 d3d11.dll 与 dxgi.dll 重命名为 .disabled 备份，下次开启可直接恢复，无需重新下载。
    /// </summary>
    private async void DxvkOptimizerToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleHandler) return;
        if (sender is not ToggleSwitch toggle) return;

        var gamePath = AppSettings.UnturnedInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowStatus("请先在设置中配置有效的 Unturned 安装路径", InfoBarSeverity.Warning);
            _suppressToggleHandler = true;
            toggle.IsChecked = false;
            _suppressToggleHandler = false;
            return;
        }

        bool wantEnable = toggle.IsChecked == true;

        // v1.6.8：兼容性警告 - GPU 不推荐 DXVK 时弹窗确认（仅首次）
        if (wantEnable && AppSettings.DxvkRecommendedByGpu == false && !AppSettings.HasShownDxvkCompatWarning)
        {
            AppSettings.HasShownDxvkCompatWarning = true;
            var result = System.Windows.MessageBox.Show(
                "⚠️ DXVK 兼容性提示\n\n" +
                "检测到您的显卡架构较老，DXVK 2.4 依赖的 Vulkan 1.3 现代扩展支持不完整，" +
                "可能导致严重降帧（如 GTX 1060 Pascal 架构实测仅 6 FPS）。\n\n" +
                "建议：关闭 DXVK，使用原生 D3D11\n\n" +
                "是否仍要启用 DXVK？",
                "DXVK 兼容性警告",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                _suppressToggleHandler = true;
                toggle.IsChecked = false;
                _suppressToggleHandler = false;
                return;
            }
        }

        var d3d11Path = Path.Combine(gamePath, DxvkD3d11Dll);
        var dxgiPath = Path.Combine(gamePath, DxvkDxgiDll);
        var d3d11DisabledPath = Path.Combine(gamePath, DxvkD3d11Disabled);
        var dxgiDisabledPath = Path.Combine(gamePath, DxvkDxgiDisabled);

        try
        {
            if (wantEnable)
            {
                if (File.Exists(d3d11Path) && File.Exists(dxgiPath))
                {
                    AppSettings.EnableDxvk = true;
                    ShowStatus("DXVK 已启用", InfoBarSeverity.Informational);
                }
                else if (File.Exists(d3d11DisabledPath) && File.Exists(dxgiDisabledPath))
                {
                    // 从 .disabled 备份恢复，无需重新下载
                    SafeMoveFile(d3d11DisabledPath, d3d11Path);
                    SafeMoveFile(dxgiDisabledPath, dxgiPath);
                    AppSettings.EnableDxvk = true;
                    ShowStatus("DXVK 极速优化已启用（从备份恢复）", InfoBarSeverity.Success);
                }
                else
                {
                    DxvkOptimizerToggle.IsEnabled = false;
                    ShowStatus("正在下载并部署 DXVK ...", InfoBarSeverity.Informational);

                    await DeployDxvkAsync(gamePath);

                    AppSettings.EnableDxvk = true;
                    ShowStatus("DXVK 极速优化已启用（DX11 ➔ Vulkan）", InfoBarSeverity.Success);
                }
            }
            else
            {
                // 关闭：重命名为 .disabled 备份，下次开启可秒恢复
                if (File.Exists(d3d11Path))
                {
                    SafeMoveFile(d3d11Path, d3d11DisabledPath);
                }
                if (File.Exists(dxgiPath))
                {
                    SafeMoveFile(dxgiPath, dxgiDisabledPath);
                }
                AppSettings.EnableDxvk = false;
                ShowStatus("DXVK 已关闭（已备份为 .disabled），游戏恢复原生 DX11 模式", InfoBarSeverity.Informational);
            }
        }
        catch (Exception ex)
        {
            _suppressToggleHandler = true;
            toggle.IsChecked = !wantEnable;
            _suppressToggleHandler = false;
            ShowStatus($"DXVK 切换失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            DxvkOptimizerToggle.IsEnabled = true;
        }
    }

    /// <summary>
    /// 异步下载 DXVK tar.gz 并流式提取 x64/d3d11.dll 和 x64/dxgi.dll 到游戏根目录。
    /// 优先国内镜像源，失败时回退官方 GitHub。
    /// </summary>
    private async Task DeployDxvkAsync(string gamePath)
    {
        var tempTarGz = Path.GetTempFileName() + ".tar.gz";

        try
        {
            Exception? lastException = null;
            bool downloaded = false;

            for (int i = 0; i < DxvkDownloadUrls.Length; i++)
            {
                string url = DxvkDownloadUrls[i];
                string sourceName = i == 0 ? "国内镜像" : "官方 GitHub";

                try
                {
                    Dispatcher.Invoke(() => ShowStatus($"正在连接 {sourceName} ...", InfoBarSeverity.Informational));

                    using var client = new HttpClient();
                    // 国内源 5 秒未建立连接即切换备用源
                    client.Timeout = i == 0 ? TimeSpan.FromSeconds(5) : TimeSpan.FromMinutes(10);

                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(tempTarGz, FileMode.Create, FileAccess.Write, FileShare.None);

                    var buffer = new byte[81920];
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    }

                    downloaded = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"[DXVK-Download] 源 {sourceName} 失败: {ex.Message}");
                    if (i < DxvkDownloadUrls.Length - 1)
                    {
                        Dispatcher.Invoke(() => ShowStatus($"{sourceName} 连接失败，切换备用源...", InfoBarSeverity.Informational));
                    }
                }
            }

            if (!downloaded)
            {
                throw new Exception($"所有下载源均失败。最后一次错误: {lastException?.Message}", lastException);
            }

            Dispatcher.Invoke(() => ShowStatus("正在解压提取 x64/d3d11.dll 与 x64/dxgi.dll ...", InfoBarSeverity.Informational));

            // 流式遍历 tar.gz，仅提取所需的两个 DLL
            int extracted = 0;
            using (var fs = new FileStream(tempTarGz, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var tarReader = new TarReader(gz))
            {
                TarEntry? entry;
                while ((entry = tarReader.GetNextEntry()) != null)
                {
                    if (entry.EntryType != TarEntryType.RegularFile) continue;
                    if (entry.DataStream == null) continue;

                    var name = entry.Name.Replace('\\', '/');

                    if (name.EndsWith("/x64/d3d11.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        await ExtractEntryToAsync(entry, Path.Combine(gamePath, DxvkD3d11Dll));
                        extracted++;
                    }
                    else if (name.EndsWith("/x64/dxgi.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        await ExtractEntryToAsync(entry, Path.Combine(gamePath, DxvkDxgiDll));
                        extracted++;
                    }
                }
            }

            if (extracted < 2
                || !File.Exists(Path.Combine(gamePath, DxvkD3d11Dll))
                || !File.Exists(Path.Combine(gamePath, DxvkDxgiDll)))
            {
                throw new Exception($"DXVK 解压完成但未找到 x64/d3d11.dll 或 x64/dxgi.dll（提取 {extracted} 个文件）");
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempTarGz))
                    File.Delete(tempTarGz);
            }
            catch
            {
                // 临时文件清理失败不影响主流程
            }
        }
    }

    /// <summary>
    /// 将 TarEntry 的 DataStream 内容写入目标文件。
    /// </summary>
    private static async Task ExtractEntryToAsync(TarEntry entry, string targetPath)
    {
        if (entry.DataStream == null) return;

        // 覆盖写：若已存在则先删除
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        await using var dst = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await entry.DataStream.CopyToAsync(dst);
    }

    private async void InstallBepInExButton_Click(object sender, RoutedEventArgs e)
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowStatus("请先在设置中配置有效的 Unturned 安装路径", InfoBarSeverity.Warning);
            return;
        }

        if (!await WpfUiDialogHelper.ConfirmAutoInstallAsync())
            return;

        if (!await WpfUiDialogHelper.ConfirmStartDownloadAsync())
            return;

        await RunBepInExDeploymentAsync(gamePath, "安装");
    }

    private async void RepairBepInExButton_Click(object sender, RoutedEventArgs e)
    {
        var gamePath = AppSettings.UnturnedInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ShowStatus("请先在设置中配置有效的 Unturned 安装路径", InfoBarSeverity.Warning);
            return;
        }

        if (!await WpfUiDialogHelper.ConfirmRepairAsync())
            return;

        await RunBepInExDeploymentAsync(gamePath, "修复");
    }

    private async Task RunBepInExDeploymentAsync(string gamePath, string operationName)
    {
        ShowInstallProgress();

        try
        {
            await DownloadAndInstallBepInExAsync(gamePath);
            RefreshBepInExStatus();
            RefreshGlobalModStatus();
            ShowStatus($"BepInEx {operationName}完成，已可以启动游戏", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"BepInEx {operationName}失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            HideInstallProgress();
        }
    }

    private void ShowInstallProgress()
    {
        LaunchGameButton.Visibility = Visibility.Collapsed;
        InstallProgressPanel.Visibility = Visibility.Visible;
        InstallBepInExButton.IsEnabled = false;
        RepairBepInExButton.IsEnabled = false;
        GlobalModToggle.IsEnabled = false;
        InstallProgressBar.Value = 0;
        InstallProgressText.Text = "准备下载...";
    }

    private void HideInstallProgress()
    {
        LaunchGameButton.Visibility = Visibility.Visible;
        InstallProgressPanel.Visibility = Visibility.Collapsed;
        InstallBepInExButton.IsEnabled = true;
        RepairBepInExButton.IsEnabled = true;
        GlobalModToggle.IsEnabled = true;
    }

    private void UpdateInstallProgress(int percentage, string message)
    {
        InstallProgressBar.Value = percentage;
        InstallProgressText.Text = message;
    }

    private async Task DownloadAndInstallBepInExAsync(string gamePath)
    {
        var tempZip = Path.GetTempFileName() + ".zip";
        try
        {
            Exception? lastException = null;
            for (int i = 0; i < BepInExDownloadUrls.Length; i++)
            {
                string url = BepInExDownloadUrls[i];
                string sourceName = i == 0 ? "国内镜像" : "官方 GitHub";

                try
                {
                    Dispatcher.Invoke(() => UpdateInstallProgress(0, $"正在连接 {sourceName}..."));

                    using var client = new HttpClient();
                    // 优先源 5 秒超时未建立连接或报错则切换备用源
                    client.Timeout = i == 0 ? TimeSpan.FromSeconds(5) : TimeSpan.FromMinutes(10);

                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    await DownloadResponseToFileAsync(response, tempZip);

                    Dispatcher.Invoke(() => UpdateInstallProgress(100, "正在解压部署..."));
                    ZipFile.ExtractToDirectory(tempZip, gamePath, overwriteFiles: true);

                    // BepInEx 解压完成后，立即从启动器嵌入式资源释放核心优化模组
                    Dispatcher.Invoke(() => UpdateInstallProgress(100, "正在释放核心优化模组..."));
                    DeployEmbeddedCoreMods(gamePath);

                    Dispatcher.Invoke(() => UpdateInstallProgress(100, "部署完成"));
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    System.Diagnostics.Debug.WriteLine($"[BepInEx-Download] 源 {sourceName} 失败: {ex.Message}");
                    if (i < BepInExDownloadUrls.Length - 1)
                    {
                        Dispatcher.Invoke(() => UpdateInstallProgress(0, $"{sourceName} 连接失败，切换备用源..."));
                    }
                }
            }

            throw new Exception($"所有下载源均失败。最后一次错误: {lastException?.Message}", lastException);
        }
        finally
        {
            try
            {
                if (File.Exists(tempZip))
                    File.Delete(tempZip);
            }
            catch
            {
                // 临时文件清理失败不影响主流程
            }
        }
    }

    private async Task DownloadResponseToFileAsync(HttpResponseMessage response, string tempZip)
    {
        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long readBytes = 0;
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            readBytes += bytesRead;

            if (totalBytes > 0)
            {
                int percentage = (int)(readBytes * 100 / totalBytes);
                Dispatcher.Invoke(() => UpdateInstallProgress(percentage, $"正在下载: {percentage}%"));
            }
        }
    }

    /// <summary>
    /// 从启动器 WPF 资源中释放核心优化模组到 BepInEx/plugins/ 目录。
    /// 包含 WaterPerfOptimizer_v1.0.dll 与 LaunchPerfOptimizer_v1.0.dll，作为启动器自带核心功能。
    /// 资源以 &lt;Resource&gt; 项嵌入到 .g.resources 中，用 pack URI 读取。
    /// 落地文件名去掉 _v1.0 后缀，保持纯净名（避免 BepInEx 重复加载版本化与未版本化副本）。
    /// </summary>
    private static void DeployEmbeddedCoreMods(string gamePath)
    {
        var pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
        Directory.CreateDirectory(pluginsPath);

        // 资源名（带版本）-> 落地名（不带版本）
        var coreMods = new[]
        {
            ("WaterPerfOptimizer_v1.0.dll", "WaterPerfOptimizer.dll"),
            ("LaunchPerfOptimizer_v1.0.dll", "LaunchPerfOptimizer.dll")
        };

        foreach (var (resourceName, deployName) in coreMods)
        {
            try
            {
                // WPF pack URI：从 .g.resources 读取嵌入的 .dll 资源（资源名在 .g.resources 中会转为小写）
                var uri = new Uri($"pack://application:,,,/Resources/{resourceName}", UriKind.Absolute);
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CoreMod] WPF 资源未找到: {resourceName}");
                    continue;
                }

                var targetPath = Path.Combine(pluginsPath, deployName);
                // 覆盖写：若已存在则先删除，确保每次部署都是最新版本
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                info.Stream.CopyTo(fs);
                info.Stream.Dispose();

                System.Diagnostics.Debug.WriteLine($"[CoreMod] 已释放: {resourceName} -> {targetPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreMod] 释放失败 {resourceName}: {ex.Message}");
            }
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
        StatusContainer.Visibility = Visibility.Visible;

        // 重置进度条为满格，重新启动 10 秒倒计时（进度条从右往左递减消失）
        StatusProgressBar.Value = 100;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    /// <summary>
    /// "一键导出"按钮：异步打包 Unity 与 BepInEx 日志到桌面，并自动弹开文件夹。
    /// </summary>
    private async void ExportLogsButton_Click(object sender, RoutedEventArgs e)
    {
        ExportLogsButton.IsEnabled = false;
        try
        {
            var exportFolder = await Task.Run(ExportDiagnosticLogs);

            // 极客小动作：自动弹开桌面上的导出文件夹
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{exportFolder}\"",
                UseShellExecute = true
            });

            AppSettings.LastSessionCrashed = false;
            RefreshCrashAlert();
            ShowStatus($"诊断日志已导出至桌面：{Path.GetFileName(exportFolder)}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"导出失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            ExportLogsButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// "忽略"按钮：将 LastSessionCrashed 置为 false 并隐藏提示条。
    /// </summary>
    private void IgnoreCrashButton_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.LastSessionCrashed = false;
        RefreshCrashAlert();
    }

    /// <summary>
    /// 收集 Unity 上一次运行日志（Player-prev.log）与 BepInEx 日志（LogOutput.log），
    /// 复制到桌面上以时间戳命名的文件夹中。返回导出文件夹的完整路径。
    /// </summary>
    private static string ExportDiagnosticLogs()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var folderName = $"Unturned_模组崩溃诊断_{timestamp}";
        var exportFolder = Path.Combine(desktopPath, folderName);
        Directory.CreateDirectory(exportFolder);

        // 1. Unity 崩溃日志（位于用户目录的 AppData/LocalLow 下）
        var unityLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "SmartlyDressedGames", "Unturned", "Player-prev.log");
        if (File.Exists(unityLogPath))
        {
            File.Copy(unityLogPath, Path.Combine(exportFolder, "Player-prev.log"), overwrite: true);
        }

        // 2. BepInEx 模组日志（位于游戏目录的 BepInEx/ 下）
        var gamePath = AppSettings.UnturnedInstallPath;
        if (!string.IsNullOrEmpty(gamePath))
        {
            var bepInExLogPath = Path.Combine(gamePath, "BepInEx", "LogOutput.log");
            if (File.Exists(bepInExLogPath))
            {
                File.Copy(bepInExLogPath, Path.Combine(exportFolder, "LogOutput.log"), overwrite: true);
            }
        }

        return exportFolder;
    }

    /// <summary>
    /// 拦截鼠标滚轮事件并手动路由到最近的 ScrollViewer 祖先。
    /// 修复：内部 Border/Card 有 Background 命中测试时，滚轮事件会被吞噬而不冒泡到 ScrollViewer。
    /// PreviewMouseWheel 是隧道事件，在子控件处理前先到达此处。
    /// 通过 VisualTreeHelper 向上递归寻找最近的 ScrollViewer 并手动滚动。
    /// </summary>
    private void CardPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DependencyObject element) return;

        DependencyObject? current = element;
        while (current != null)
        {
            if (current is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }
}