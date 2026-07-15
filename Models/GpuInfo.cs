namespace UnturnedModManager.Models;

public enum GpuVendor
{
    Unknown,
    Nvidia,
    Amd,
    Intel
}

public enum GpuArchitecture
{
    Unknown,
    Pascal,         // NVIDIA GTX 10 系（2016）
    Turing,         // NVIDIA GTX 16 / RTX 20 系（2018）
    Ampere,         // NVIDIA RTX 30 系（2020）
    AdaLovelace,    // NVIDIA RTX 40 系（2022）
    Blackwell,      // NVIDIA RTX 50 系（2025）

    Polaris,        // AMD RX 400/500 系（2016/2017）
    Vega,           // AMD RX Vega 系（2017）
    Rdna1,          // AMD RX 5000 系（2019）
    Rdna2,          // AMD RX 6000 系（2020）
    Rdna3,          // AMD RX 7000 系（2022）
    Rdna4,          // AMD RX 9000 系（2025）

    Alchemist,      // Intel Arc A 系（2022）
    Battlemage      // Intel Arc B 系（2024）
}

public enum DxvkRecommendation
{
    Recommended,
    Neutral,
    NotRecommended
}

public class GpuInfo
{
    public string Name { get; set; } = string.Empty;
    public GpuVendor Vendor { get; set; }
    public GpuArchitecture Architecture { get; set; }
    public DxvkRecommendation DxvkRecommendation { get; set; }

    public string VendorName => Vendor switch
    {
        GpuVendor.Nvidia => "NVIDIA",
        GpuVendor.Amd => "AMD",
        GpuVendor.Intel => "Intel",
        _ => "未知"
    };

    public string ArchitectureName => Architecture switch
    {
        GpuArchitecture.Pascal => "Pascal (2016)",
        GpuArchitecture.Turing => "Turing (2018)",
        GpuArchitecture.Ampere => "Ampere (2020)",
        GpuArchitecture.AdaLovelace => "Ada Lovelace (2022)",
        GpuArchitecture.Blackwell => "Blackwell (2025)",
        GpuArchitecture.Polaris => "Polaris (2016)",
        GpuArchitecture.Vega => "Vega (2017)",
        GpuArchitecture.Rdna1 => "RDNA 1 (2019)",
        GpuArchitecture.Rdna2 => "RDNA 2 (2020)",
        GpuArchitecture.Rdna3 => "RDNA 3 (2022)",
        GpuArchitecture.Rdna4 => "RDNA 4 (2025)",
        GpuArchitecture.Alchemist => "Alchemist (2022)",
        GpuArchitecture.Battlemage => "Battlemage (2024)",
        _ => "未知"
    };

    public string RecommendationText => DxvkRecommendation switch
    {
        DxvkRecommendation.Recommended => "✅ 推荐 DXVK",
        DxvkRecommendation.Neutral => "⚠️ DXVK 兼容性一般",
        DxvkRecommendation.NotRecommended => "❌ 不推荐 DXVK",
        _ => "未知"
    };

    public string RecommendationDetail => DxvkRecommendation switch
    {
        DxvkRecommendation.Recommended => "您的显卡架构对 Vulkan 支持良好，DXVK 可带来性能提升",
        DxvkRecommendation.Neutral => "DXVK 可启用但提升有限，建议对比测试后决定",
        DxvkRecommendation.NotRecommended => "您的显卡架构较老，Vulkan 扩展支持不完整，DXVK 可能严重降帧。建议使用原生 D3D11",
        _ => string.Empty
    };
}
