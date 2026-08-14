using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UnturnedModManager.Models;

public sealed class ModItem : INotifyPropertyChanged
{
    private bool _isEnabled;
    private int? _communityModId;
    private string _communityTitle = "";
    private string _remoteVersion = "";
    public string DisplayTitle => string.IsNullOrWhiteSpace(CommunityTitle) ? AssemblyName : CommunityTitle;
    public string AssemblyName { get; set; } = "";
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string InstallTime { get; set; } = "";
    public string InstalledVersion { get; set; } = "";
    public int? CommunityModId { get => _communityModId; set { if (_communityModId == value) return; _communityModId = value; Notify(); Notify(nameof(IsCommunityMatched)); Notify(nameof(SourceText)); } }
    public string CommunityTitle { get => _communityTitle; set { if (_communityTitle == value) return; _communityTitle = value; Notify(); Notify(nameof(DisplayTitle)); } }
    public string RemoteVersion { get => _remoteVersion; set { if (_remoteVersion == value) return; _remoteVersion = value; Notify(); Notify(nameof(HasUpdate)); Notify(nameof(VersionText)); } }
    public bool IsCommunityManaged { get; set; }
    public bool IsCommunityMatched => CommunityModId.HasValue;
    public bool HasUpdate => IsCommunityManaged && !string.IsNullOrWhiteSpace(RemoteVersion) && !VersionsEqual(InstalledVersion, RemoteVersion);
    public string SourceText => IsCommunityManaged ? "社区安装" : IsCommunityMatched ? "手动安装 · 已匹配社区" : "手动安装";
    public string VersionText => string.IsNullOrWhiteSpace(InstalledVersion) ? InstallTime : $"已安装 {InstalledVersion}" + (HasUpdate ? $" · 可更新至 {RemoteVersion}" : "");
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value) return; _isEnabled = value; Notify(); } }
    private static bool VersionsEqual(string a, string b) => a.Trim().TrimStart('v', 'V').Equals(b.Trim().TrimStart('v', 'V'), StringComparison.OrdinalIgnoreCase);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
