using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

public sealed class CommunityApiClient : IDisposable
{
    public const string BaseUrl = "https://unmod.online";
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private readonly CommunityCacheService _cache;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public bool LastResponseWasCached { get; private set; }

    public CommunityApiClient() : this(new CommunityCacheService()) { }

    public CommunityApiClient(CommunityCacheService cache)
    {
        _cache = cache;
        _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies, UseCookies = true })
        {
            BaseAddress = new Uri(BaseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
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
            await _cache.WriteAsync(cacheKey, detail, token);
            return detail;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            var cached = await _cache.ReadStaleAsync<CommunityModDetail>(cacheKey, token);
            if (cached is null) throw;
            LastResponseWasCached = true;
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

    public async Task<DownloadedMod> DownloadAsync(int id, CancellationToken token = default)
    {
        RefreshAuthentication();
        using var response = await _http.GetAsync($"api/mods/{id}/file", token);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync(token);
            throw new HttpRequestException($"下载失败（{(int)response.StatusCode}）：{message}");
        }
        var bytes = await response.Content.ReadAsByteArrayAsync(token);
        if (bytes.Length == 0) throw new InvalidDataException("下载文件为空。");
        return new DownloadedMod(ParseFileName(response.Content.Headers.ContentDisposition) ?? $"mod-{id}.zip", bytes);
    }

    private static string? ParseFileName(ContentDispositionHeaderValue? value) =>
        value?.FileNameStar?.Trim('"') ?? value?.FileName?.Trim('"');

    private void RefreshAuthentication()
    {
        if (string.IsNullOrWhiteSpace(AppSettings.CommunityAuthToken)) return;
        _cookies.SetCookies(new Uri(BaseUrl), $"token={AppSettings.CommunityAuthToken}; path=/");
    }

    public void Dispose() => _http.Dispose();

    private sealed class CategoriesResponse { public List<CommunityCategory> Categories { get; set; } = []; }
    private sealed class ModsResponse
    {
        public List<CommunityMod> Mods { get; set; } = [];
        public int Pages { get; set; }
    }
    private sealed class ModDetailResponse { public CommunityModDetail? Mod { get; set; } }
}

public sealed record DownloadedMod(string FileName, byte[] Content);
