using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnturnedModManager.Models;

[JsonConverter(typeof(LocalizedTextConverter))]
public sealed class LocalizedText
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    internal IReadOnlyDictionary<string, string> Values => _values;

    public string Pick(string locale = "zh")
    {
        if (_values.TryGetValue(locale, out var exact) && !string.IsNullOrWhiteSpace(exact))
            return exact.Trim();
        if (_values.TryGetValue("zh", out var zh) && !string.IsNullOrWhiteSpace(zh))
            return zh.Trim();
        if (_values.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en))
            return en.Trim();
        return _values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
    }

    internal void Set(string key, string value) => _values[key] = value;
}

public sealed class LocalizedTextConverter : JsonConverter<LocalizedText>
{
    public override LocalizedText Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var result = new LocalizedText();
        if (reader.TokenType == JsonTokenType.String)
        {
            result.Set("zh", reader.GetString() ?? "");
            return result;
        }
        if (reader.TokenType != JsonTokenType.StartObject)
            return result;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;
            var key = reader.GetString() ?? "";
            reader.Read();
            if (reader.TokenType == JsonTokenType.String)
                result.Set(key, reader.GetString() ?? "");
            else
                reader.Skip();
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, LocalizedText value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var pair in value.Values)
            writer.WriteString(pair.Key, pair.Value);
        writer.WriteEndObject();
    }
}

public class CommunityMod
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public LocalizedText Title { get; set; } = new();
    [JsonPropertyName("description")] public LocalizedText? Description { get; set; }
    [JsonPropertyName("body")] public LocalizedText? Body { get; set; }
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("author_name")] public string AuthorName { get; set; } = "";
    [JsonPropertyName("cover_url")] public string? CoverUrl { get; set; }
    [JsonPropertyName("has_file")] public bool HasFile { get; set; }
    [JsonPropertyName("github_repo")] public string? GitHubRepository { get; set; }
    [JsonPropertyName("downloads")] public int Downloads { get; set; }
    [JsonPropertyName("like_count")] public int LikeCount { get; set; }

    [JsonIgnore] public string DisplayTitle => Title.Pick();
    [JsonIgnore] public string DisplayDescription => Description?.Pick() ?? "暂无简介";
    [JsonIgnore] public virtual string EffectiveVersion => Version;
    [JsonIgnore] public string Meta => $"{AuthorName}  ·  {FormatVersion(EffectiveVersion)}  ·  {Downloads:N0} 次下载";
    [JsonIgnore] public string CategoryDisplay => Category switch
    {
        "weapon" => "武器", "survival" => "生存", "map" => "地图", "vehicle" => "载具",
        "interface" => "界面", "other" => "其他", _ => string.IsNullOrWhiteSpace(Category) ? "其他" : Category
    };
    [JsonIgnore] public string DownloadLabel => $"{Downloads:N0} 次下载";
    [JsonIgnore] public bool UsesGitHubRelease => !string.IsNullOrWhiteSpace(GitHubRepository);
    private static string FormatVersion(string version) =>
        version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : $"v{version}";
}

public sealed class CommunityModDetail : CommunityMod
{
    [JsonPropertyName("dependencies")] public List<CommunityDependency> Dependencies { get; set; } = [];
    [JsonPropertyName("file_size")] public long FileSize { get; set; }
    [JsonIgnore] public string? GitHubReleaseTag { get; private set; }
    [JsonIgnore] public long GitHubReleaseAssetSize { get; private set; }
    [JsonIgnore] public bool IsGitHubReleaseResolved { get; private set; }
    [JsonIgnore] public override string EffectiveVersion => IsGitHubReleaseResolved ? GitHubReleaseTag! : Version;
    [JsonIgnore] public long EffectiveFileSize => IsGitHubReleaseResolved ? GitHubReleaseAssetSize : FileSize;
    [JsonIgnore] public string DisplayBody => Body?.Pick() is { Length: > 0 } body ? body : DisplayDescription;
    [JsonIgnore] public string DependencySummary => Dependencies.Count == 0
        ? "无额外依赖"
        : $"需要 {Dependencies.Count} 个依赖，将自动安装";
    [JsonIgnore] public string DownloadSourceText => UsesGitHubRelease
        ? IsGitHubReleaseResolved
            ? $"GitHub 最新 Release · {GitHubReleaseTag}"
            : "GitHub Release（下载时读取 latest）"
        : "unmod.online 社区包";

    public void ApplyGitHubRelease(string tagName, long assetSize)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;
        GitHubReleaseTag = tagName.Trim();
        GitHubReleaseAssetSize = assetSize > 0 ? assetSize : FileSize;
        IsGitHubReleaseResolved = true;
    }
}

public sealed class CommunityDependency
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("title")] public LocalizedText Title { get; set; } = new();
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonIgnore] public string DisplayTitle => Title.Pick();
}

public sealed class CommunityCategory
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("name_zh")] public string NameZh { get; set; } = "";
    [JsonPropertyName("name_en")] public string NameEn { get; set; } = "";
    [JsonIgnore] public string DisplayName => string.IsNullOrWhiteSpace(NameZh) ? NameEn : NameZh;
}

public sealed class InstalledCommunityMod
{
    public int RemoteId { get; set; }
    public string Title { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
    public List<InstalledCommunityFile> Files { get; set; } = [];
}

public sealed class InstalledCommunityFile
{
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string? BackupPath { get; set; }
}
