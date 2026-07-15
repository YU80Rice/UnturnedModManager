using System.Management;
using System.Text.RegularExpressions;
using UnturnedModManager.Models;

namespace UnturnedModManager.Helpers;

/// <summary>
/// 通过 WMI Win32_VideoController 枚举显卡，并按显卡名称识别架构代际与 DXVK 推荐度。
/// v1.6.8 新增：针对 Pascal / Polaris 等老架构默认关闭 DXVK，避免 GTX 1060 等老卡严重降帧。
/// </summary>
public static class GpuDetector
{
    /// <summary>
    /// 枚举系统中所有显卡，返回分类后的 GpuInfo 列表。
    /// </summary>
    public static List<GpuInfo> DetectAll()
    {
        var result = new List<GpuInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    result.Add(Classify(name));
                }
            }
        }
        catch
        {
            // WMI 查询失败时静默降级，返回空列表
        }
        return result;
    }

    /// <summary>
    /// 返回主显卡（优先独显，无独显时返回第一个）。
    /// 用于在 UI 上展示"当前显卡"与 DXVK 推荐度。
    /// </summary>
    public static GpuInfo DetectPrimary()
    {
        var all = DetectAll();
        if (all.Count == 0) return new GpuInfo();

        // 优先返回非集显的独显
        foreach (var gpu in all)
        {
            if (!IsIntegratedGpu(gpu.Name))
                return gpu;
        }
        return all[0];
    }

    /// <summary>
    /// 判断是否为集显（Intel UHD/Iris、AMD Radeon Graphics/Vega 集显变种）。
    /// 集显不参与"独显优先"选择逻辑。
    /// </summary>
    private static bool IsIntegratedGpu(string name)
    {
        var n = name.ToUpperInvariant();
        return n.Contains("INTEL(R) UHD")
            || n.Contains("INTEL(R) IRIS")
            || n.Contains("INTEL(R) HD GRAPHICS")
            || n.Contains("AMD RADEON(TM) GRAPHICS")
            || n.Contains("AMD RADEON(TM) VEGA") && !n.Contains("RX VEGA")
            || n.Contains("MICROSOFT BASIC RENDER DRIVER");
    }

    /// <summary>
    /// 按显卡名称识别厂商、架构代际、DXVK 推荐度。
    /// 识别规则覆盖 NVIDIA（含 RTX 50 系 Blackwell）、AMD（含 RDNA 4）、Intel Arc 全系。
    /// </summary>
    public static GpuInfo Classify(string name)
    {
        var info = new GpuInfo { Name = name };
        var upper = name.ToUpperInvariant();

        if (upper.Contains("NVIDIA") || upper.Contains("GEFORCE"))
        {
            info.Vendor = GpuVendor.Nvidia;
            ClassifyNvidia(upper, info);
        }
        else if (upper.Contains("AMD") || upper.Contains("RADEON"))
        {
            info.Vendor = GpuVendor.Amd;
            ClassifyAmd(upper, info);
        }
        else if (upper.Contains("INTEL"))
        {
            info.Vendor = GpuVendor.Intel;
            ClassifyIntel(upper, info);
        }
        else
        {
            info.Vendor = GpuVendor.Unknown;
            info.DxvkRecommendation = DxvkRecommendation.Neutral;
        }

        return info;
    }

    private static void ClassifyNvidia(string upper, GpuInfo info)
    {
        // RTX 50 系 (Blackwell, 2025)
        if (Regex.IsMatch(upper, @"RTX\s*5\d{2,3}"))
        {
            info.Architecture = GpuArchitecture.Blackwell;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // RTX 40 系 (Ada Lovelace, 2022)
        else if (Regex.IsMatch(upper, @"RTX\s*4\d{2,3}"))
        {
            info.Architecture = GpuArchitecture.AdaLovelace;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // RTX 30 系 (Ampere, 2020)
        else if (Regex.IsMatch(upper, @"RTX\s*3\d{2,3}"))
        {
            info.Architecture = GpuArchitecture.Ampere;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // RTX 20 系 (Turing, 2018)
        else if (Regex.IsMatch(upper, @"RTX\s*2\d{2,3}"))
        {
            info.Architecture = GpuArchitecture.Turing;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // GTX 16 系 (Turing, 2018) - 无光追核心，DXVK 一般
        else if (Regex.IsMatch(upper, @"GTX\s*16\d{2}"))
        {
            info.Architecture = GpuArchitecture.Turing;
            info.DxvkRecommendation = DxvkRecommendation.Neutral;
        }
        // GTX 10 系 (Pascal, 2016) - Vulkan 1.3 扩展支持不完整，DXVK 严重降帧
        else if (Regex.IsMatch(upper, @"GTX\s*10\d{2}"))
        {
            info.Architecture = GpuArchitecture.Pascal;
            info.DxvkRecommendation = DxvkRecommendation.NotRecommended;
        }
        // GTX 9 系及更早 (Maxwell/Kepler) - 不推荐
        else if (Regex.IsMatch(upper, @"GTX\s*9\d{2}") || Regex.IsMatch(upper, @"GTX\s*[78]\d{2}"))
        {
            info.Architecture = GpuArchitecture.Pascal;  // 兜底归类
            info.DxvkRecommendation = DxvkRecommendation.NotRecommended;
        }
        else
        {
            info.Architecture = GpuArchitecture.Unknown;
            info.DxvkRecommendation = DxvkRecommendation.Neutral;
        }
    }

    private static void ClassifyAmd(string upper, GpuInfo info)
    {
        // RX 9000 系 (RDNA 4, 2025)
        if (Regex.IsMatch(upper, @"RX\s*9\d{3}"))
        {
            info.Architecture = GpuArchitecture.Rdna4;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // RX 7000 系 (RDNA 3, 2022)
        else if (Regex.IsMatch(upper, @"RX\s*7\d{3}"))
        {
            info.Architecture = GpuArchitecture.Rdna3;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // RX 6000 系 (RDNA 2, 2020)
        else if (Regex.IsMatch(upper, @"RX\s*6\d{3}"))
        {
            info.Architecture = GpuArchitecture.Rdna2;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // RX 5000 系 (RDNA 1, 2019)
        else if (Regex.IsMatch(upper, @"RX\s*5\d{3}"))
        {
            info.Architecture = GpuArchitecture.Rdna1;
            info.DxvkRecommendation = DxvkRecommendation.Neutral;
        }
        // RX Vega 系 (Vega, 2017)
        else if (upper.Contains("VEGA"))
        {
            info.Architecture = GpuArchitecture.Vega;
            info.DxvkRecommendation = DxvkRecommendation.Neutral;
        }
        // RX 400/500 系 (Polaris, 2016/2017) - 不推荐
        else if (Regex.IsMatch(upper, @"RX\s*[45]\d{2,3}") && !Regex.IsMatch(upper, @"RX\s*5\d{3}"))
        {
            info.Architecture = GpuArchitecture.Polaris;
            info.DxvkRecommendation = DxvkRecommendation.NotRecommended;
        }
        // Radeon HD 系列等更老显卡
        else if (upper.Contains("RADEON HD") || Regex.IsMatch(upper, @"RADEON\s*\w+\s+HD"))
        {
            info.Architecture = GpuArchitecture.Polaris;  // 兜底归类
            info.DxvkRecommendation = DxvkRecommendation.NotRecommended;
        }
        else
        {
            info.Architecture = GpuArchitecture.Unknown;
            info.DxvkRecommendation = DxvkRecommendation.Neutral;
        }
    }

    private static void ClassifyIntel(string upper, GpuInfo info)
    {
        // Arc B 系 (Battlemage, 2024)
        if (Regex.IsMatch(upper, @"ARC\s*B\d{3}"))
        {
            info.Architecture = GpuArchitecture.Battlemage;
            info.DxvkRecommendation = DxvkRecommendation.Recommended;
        }
        // Arc A 系 (Alchemist, 2022)
        else if (Regex.IsMatch(upper, @"ARC\s*A\d{3}"))
        {
            info.Architecture = GpuArchitecture.Alchemist;
            info.DxvkRecommendation = DxvkRecommendation.Neutral;
        }
        // Intel 集显（UHD/Iris/Xe 等）- 不推荐 DXVK
        else
        {
            info.Architecture = GpuArchitecture.Unknown;
            info.DxvkRecommendation = DxvkRecommendation.NotRecommended;
        }
    }
}
