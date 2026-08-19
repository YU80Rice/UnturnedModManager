using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

public sealed class CommunityApiClient : IDisposable
{
    public const string BaseUrl = "https://unmod.online";
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private readonly CommunityCacheService _cache;
    private readonly bool _ownsHttp;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public bool LastResponseWasCached { get; private set; }

    public CommunityApiClient() : this(new CommunityCacheService()) { }

    public CommunityApiClient(CommunityCacheService cache, HttpClient? http = null)
    {
        _cache = cache;
        _ownsHttp = http is null;
        _http = http ?? new HttpClient(new HttpClientHandler { CookieContainer = _cookies, UseCookies = true })
        {
            BaseAddress = new Uri(BaseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(BaseUrl + "/");
        RefreshAuthentication();
    }

    public async Task<IReadOnlyList<CommunityCategory>> GetCategoriesAsync(CancellationToken token = default, bool forceRefresh = false)
    {
        LastResponseWasCached = false;
        if (!forceRefresh)
        {
            var cached = await _cache.ReadAsync<List<CommunityCategory>>("categories", TimeSpan.FromHours(24), token);
            if (cached is not null)
            {
                LastResponseWasCached = true;
                return cached;
            }
        }
        RefreshAuthentication();
        try
        {
            var payload = await _http.GetFromJsonAsync<CategoriesResponse>("api/categories", JsonOptions, token);
            var categories = payload?.Categories ?? [];
            await _cache.WriteAsync("categories", categories, token);
            return categories;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            var cached = await _cache.ReadStaleAsync<List<CommunityCategory>>("categories", token);
            if (cached is null) throw;
            LastResponseWasCached = true;
            return cached;
        }
    }

    public async Task<IReadOnlyList<CommunityMod>> GetModsAsync(
        string? category, string? search, string sort, CancellationToken token = default, bool forceRefresh = false)
    {
        LastResponseWasCached = false;
        RefreshAuthentication();
        var cacheKey = $"mods|category={category}|search={search?.Trim()}|sort={sort}";
        if (!forceRefresh)
        {
            var fresh = await _cache.ReadAsync<List<CommunityMod>>(cacheKey, TimeSpan.FromMinutes(10), token);
            if (fresh is not null)
            {
                LastResponseWasCached = true;
                return fresh;
            }
        }
        try
        {
            var result = new List<CommunityMod>();
            var page = 1;
            var pages = 1;
            do
            {
                var query = new List<string> { $"page={page}", $"sort={Uri.EscapeDataString(sort)}" };
                if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
                if (!string.IsNullOrWhiteSpace(search)) query.Add($"q={Uri.EscapeDataString(search.Trim())}");
                var response = await _http.GetFromJsonAsync<ModsResponse>(
                    $"api/mods?{string.Join('&', query)}", JsonOptions, token);
                if (response is null) throw new InvalidDataException("社区返回了无效数据。");
                result.AddRange(response.Mods);
                pages = Math.Max(1, response.Pages);
                page++;
            } while (page <= pages);
            await _cache.WriteAsync(cacheKey, result, token);
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            var cached = await _cache.ReadStaleAsync<List<CommunityMod>>(cacheKey, token);
            if (cached is null) throw;
            LastResponseWasCached = true;
            return cached;
        }
    }

    public async Task<CommunityModDetail> GetModAsync(int id, CancellationToken token = default)
    {
        LastResponseWasCached = false;
        RefreshAuthentication();
        var cacheKey = $"detail|{id}";
        try
        {
            var response = await _http.GetFromJsonAsync<ModDetailResponse>($"api/mods/{id}", JsonOptions, token);
            var detail = response?.Mod ?? throw new InvalidDataException("无法读取 Mod 详情。");
            await PopulateGitHubReleaseAsync(detail, token);
            await _cache.WriteAsync(cacheKey, detail, token);
            return detail;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            var cached = await _cache.ReadStaleAsync<CommunityModDetail>(cacheKey, token);
            if (cached is null) throw;
            LastResponseWasCached = true;
            await PopulateGitHubReleaseAsync(cached, token);
            return cached;
        }
    }

    public async Task<CommunityMod?> FindBestMatchAsync(string localName, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(localName)) return null;
        var candidates = await GetModsAsync(null, localName, "newest", token);
        var key = NormalizeIdentity(localName);
        return candidates.FirstOrDefault(mod => NormalizeIdentity(mod.DisplayTitle) == key)
            ?? candidates.FirstOrDefault(mod => NormalizeIdentity(mod.DisplayTitle).Contains(key, StringComparison.OrdinalIgnoreCase)
                || key.Contains(NormalizeIdentity(mod.DisplayTitle), StringComparison.OrdinalIgnoreCase))
            ?? (key.Length >= 5 ? candidates.FirstOrDefault(mod => NormalizeIdentity(mod.DisplayDescription).Contains(key, StringComparison.OrdinalIgnoreCase)) : null);
    }

    private static string NormalizeIdentity(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public Task<DownloadedMod> DownloadAsync(int id, CancellationToken token = default) =>
        DownloadAsync(id, null, token);

    /// <summary>以流方式读取社区包，避免下载时没有进度且为完整响应额外缓冲一次内存。</summary>
    public async Task<DownloadedMod> DownloadAsync(
        int id,
        IProgress<DownloadProgress>? progress,
        CancellationToken token = default)
    {
        return await DownloadCommunityPackageAsync(id, progress, token);
    }

    /// <summary>
    /// GitHub Release 来源直接读取仓库的 latest Release；GitHub 暂时不可用时，
    /// 回退到需要社区会话的原有社区包端点。
    /// </summary>
    public async Task<DownloadedMod> DownloadAsync(
        CommunityModDetail mod,
        IProgress<DownloadProgress>? progress,
        CancellationToken token = default)
    {
        if (TryParseGitHubRepository(mod.GitHubRepository, out var repository))
        {
            try
            {
                var release = await GetGitHubLatestReleaseAsync(repository, token);
                var downloaded = await DownloadGitHubAssetAsync(release, progress, token);
                mod.ApplyGitHubRelease(release.TagName, release.Asset.Size);
                return downloaded;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (GitHubAssetIntegrityException) { throw; }
            catch (InvalidDataException) { throw; }
            catch (JsonException) { throw; }
            catch (HttpRequestException)
            {
                // GitHub API 限流或短时故障不应阻断已登录用户的社区包下载。
            }
            catch (TaskCanceledException) { }
            catch (IOException) { }
        }

        return await DownloadCommunityPackageAsync(mod.Id, progress, token, mod.Version);
    }

    private async Task<DownloadedMod> DownloadCommunityPackageAsync(
        int id,
        IProgress<DownloadProgress>? progress,
        CancellationToken token,
        string? sourceVersion = null)
    {
        RefreshAuthentication();
        using var response = await _http.GetAsync(
            $"api/mods/{id}/file",
            HttpCompletionOption.ResponseHeadersRead,
            token);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(token);
            throw new HttpRequestException($"下载失败（{(int)response.StatusCode}）：{message}");
        }
        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var target = totalBytes is > 0 && totalBytes <= int.MaxValue
            ? new MemoryStream((int)totalBytes.Value)
            : new MemoryStream();
        var buffer = new byte[80 * 1024];
        long received = 0;
        int count;
        progress?.Report(new DownloadProgress(0, totalBytes));
        while ((count = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, count), token);
            received += count;
            progress?.Report(new DownloadProgress(received, totalBytes));
        }

        if (received == 0) throw new InvalidDataException("下载文件为空。");
        return new DownloadedMod(
            ParseFileName(response.Content.Headers.ContentDisposition) ?? $"mod-{id}.zip",
            target.ToArray(),
            "unmod.online 社区包",
            sourceVersion);
    }

    private async Task PopulateGitHubReleaseAsync(CommunityModDetail detail, CancellationToken token)
    {
        if (!TryParseGitHubRepository(detail.GitHubRepository, out var repository)) return;
        try
        {
            var release = await GetGitHubLatestReleaseAsync(repository, token);
            detail.ApplyGitHubRelease(release.TagName, release.Asset.Size);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch
        {
            // 社区详情和离线缓存仍可用；安装时会再尝试 GitHub，并在必要时回退社区包。
        }
    }

    private async Task<GitHubLatestRelease> GetGitHubLatestReleaseAsync(
        GitHubRepository repository,
        CancellationToken token)
    {
        using var request = CreateGitHubRequest(
            HttpMethod.Get,
            $"https://api.github.com/repos/{repository.Owner}/{repository.Name}/releases/latest");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(JsonOptions, token)
            ?? throw new InvalidDataException("GitHub latest Release 返回为空。");
        if (string.IsNullOrWhiteSpace(release.TagName))
            throw new InvalidDataException("GitHub latest Release 缺少版本标签。");

        var packageAssets = release.Assets
            .Where(asset => asset.Size > 0 && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (packageAssets.Count != 1)
            throw new InvalidDataException(packageAssets.Count == 0
                ? "GitHub latest Release 中没有可安装的 ZIP 包。"
                : "GitHub latest Release 中包含多个 ZIP 包，无法安全判断应安装哪个包。");

        var asset = packageAssets[0];
        if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUrl)
            || !downloadUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !IsOfficialGitHubReleaseAsset(downloadUrl, repository))
            throw new InvalidDataException("GitHub Release 资产地址不属于对应仓库，已拒绝下载。");
        if (!string.IsNullOrWhiteSpace(asset.Digest)
            && LauncherUpdateService.NormalizeSha256Digest(asset.Digest) is null)
            throw new GitHubAssetIntegrityException("GitHub Release SHA-256 摘要格式无效，已拒绝下载。");

        return new GitHubLatestRelease(release.TagName.Trim(), asset, downloadUrl);
    }

    private async Task<DownloadedMod> DownloadGitHubAssetAsync(
        GitHubLatestRelease release,
        IProgress<DownloadProgress>? progress,
        CancellationToken token)
    {
        using var request = CreateGitHubRequest(HttpMethod.Get, release.DownloadUrl.AbsoluteUri);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? release.Asset.Size;
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var target = release.Asset.Size <= int.MaxValue
            ? new MemoryStream((int)release.Asset.Size)
            : new MemoryStream();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[80 * 1024];
        long received = 0;
        int count;
        progress?.Report(new DownloadProgress(0, totalBytes));
        while ((count = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, count), token);
            hash.AppendData(buffer, 0, count);
            received += count;
            progress?.Report(new DownloadProgress(received, totalBytes));
        }

        if (received == 0) throw new InvalidDataException("GitHub Release 文件为空。");
        if (release.Asset.Size > 0 && received != release.Asset.Size)
            throw new GitHubAssetIntegrityException("GitHub Release 文件大小与 API 元数据不匹配，已拒绝安装。");

        var expectedSha256 = LauncherUpdateService.NormalizeSha256Digest(release.Asset.Digest);
        if (!string.IsNullOrWhiteSpace(release.Asset.Digest) && expectedSha256 is null)
            throw new GitHubAssetIntegrityException("GitHub Release SHA-256 摘要格式无效，已拒绝安装。");
        if (expectedSha256 is not null)
        {
            var actualHash = hash.GetHashAndReset();
            var expectedHash = Convert.FromHexString(expectedSha256);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                throw new GitHubAssetIntegrityException("GitHub Release 文件 SHA-256 校验失败，已拒绝安装。");
        }

        return new DownloadedMod(release.Asset.Name, target.ToArray(), "GitHub 最新 Release", release.TagName);
    }

    private static string? ParseFileName(ContentDispositionHeaderValue? value) =>
        value?.FileNameStar?.Trim('"') ?? value?.FileName?.Trim('"');

    private static bool TryParseGitHubRepository(string? value, out GitHubRepository repository)
    {
        repository = default;
        var normalized = value?.Trim() ?? "";
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var url))
        {
            if (!url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                return false;
            normalized = url.AbsolutePath.Trim('/');
        }

        normalized = normalized.TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IsGitHubName(parts[0]) || !IsGitHubName(parts[1])) return false;
        repository = new GitHubRepository(parts[0], parts[1]);
        return true;
    }

    private static bool IsGitHubName(string value) => value.Length is > 0 and <= 100
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static bool IsOfficialGitHubReleaseAsset(Uri assetUrl, GitHubRepository repository) =>
        assetUrl.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        && assetUrl.AbsolutePath.StartsWith(
            $"/{repository.Owner}/{repository.Name}/releases/download/",
            StringComparison.OrdinalIgnoreCase);

    private static HttpRequestMessage CreateGitHubRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd("UnturnedModManager/2.1 (+https://github.com/YU80Rice/UnturnedModManager)");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }

    private void RefreshAuthentication()
    {
        if (string.IsNullOrWhiteSpace(AppSettings.CommunityAuthToken)) return;
        _cookies.SetCookies(new Uri(BaseUrl), $"token={AppSettings.CommunityAuthToken}; path=/");
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }

    private sealed class CategoriesResponse { public List<CommunityCategory> Categories { get; set; } = []; }
    private sealed class ModsResponse
    {
        public List<CommunityMod> Mods { get; set; } = [];
        public int Pages { get; set; }
    }
    private sealed class ModDetailResponse { public CommunityModDetail? Mod { get; set; } }
    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("assets")] public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }

    private readonly record struct GitHubRepository(string Owner, string Name);
    private sealed record GitHubLatestRelease(string TagName, GitHubReleaseAsset Asset, Uri DownloadUrl);
    private sealed class GitHubAssetIntegrityException(string message) : IOException(message);
}

public sealed record DownloadedMod(
    string FileName,
    byte[] Content,
    string Source = "unmod.online 社区包",
    string? SourceVersion = null);
