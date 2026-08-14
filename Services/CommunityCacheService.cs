using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnturnedModManager.Services;

/// <summary>
/// 社区元数据缓存。只缓存分类、列表和详情，不缓存账户令牌或插件压缩包。
/// </summary>
public sealed class CommunityCacheService
{
    private readonly string _cacheDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public CommunityCacheService(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnturnedModManager", "cache", "community");
    }

    public async Task<T?> ReadAsync<T>(string key, TimeSpan maxAge, CancellationToken token = default)
    {
        var envelope = await ReadEnvelopeAsync<T>(key, token);
        if (envelope is null || DateTimeOffset.UtcNow - envelope.SavedAt > maxAge)
            return default;
        return envelope.Value;
    }

    public async Task<T?> ReadStaleAsync<T>(string key, CancellationToken token = default)
    {
        var envelope = await ReadEnvelopeAsync<T>(key, token);
        if (envelope is null) return default;
        return envelope.Value;
    }

    public async Task WriteAsync<T>(string key, T value, CancellationToken token = default)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);
            var path = GetPath(key);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var envelope = new CacheEnvelope<T> { SavedAt = DateTimeOffset.UtcNow, Value = value };
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(envelope, JsonOptions), Encoding.UTF8, token);
            File.Move(temporary, path, true);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* 缓存失败不应阻断在线功能。 */ }
    }

    private async Task<CacheEnvelope<T>?> ReadEnvelopeAsync<T>(string key, CancellationToken token)
    {
        try
        {
            var path = GetPath(key);
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path, token);
            return JsonSerializer.Deserialize<CacheEnvelope<T>>(json, JsonOptions);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private string GetPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_cacheDirectory, hash + ".json");
    }

    private sealed class CacheEnvelope<T>
    {
        public DateTimeOffset SavedAt { get; set; }
        public T? Value { get; set; }
    }
}
