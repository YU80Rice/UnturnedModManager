using System.IO;
using System.Net.Http;

namespace UnturnedModManager.Services;

/// <summary>
/// 一个可重试的下载源。需要社区登录的源会通过当前已保存的 unmod.online 会话令牌发送 Cookie。
/// </summary>
public sealed record DownloadSource(
    string Name,
    string Url,
    TimeSpan Timeout,
    bool RequiresCommunityAuth = false);
public sealed record OperationProgress(int Percentage, string Message);

public sealed class HttpDownloadService
{
    private readonly Func<string?> _communityTokenProvider;
    private readonly Func<TimeSpan, HttpClient> _clientFactory;

    public HttpDownloadService(
        Func<string?>? communityTokenProvider = null,
        Func<TimeSpan, HttpClient>? clientFactory = null)
    {
        _communityTokenProvider = communityTokenProvider ?? (() => AppSettings.CommunityAuthToken);
        _clientFactory = clientFactory ?? (timeout => new HttpClient { Timeout = timeout });
    }

    public async Task DownloadAsync(
        IReadOnlyList<DownloadSource> sources,
        string destination,
        IProgress<OperationProgress>? progress = null,
        CancellationToken token = default)
    {
        Exception? lastError = null;
        var skippedAuthenticatedSource = false;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            string? communityToken = null;
            try
            {
                if (source.RequiresCommunityAuth)
                {
                    communityToken = _communityTokenProvider();
                    if (string.IsNullOrWhiteSpace(communityToken))
                    {
                        skippedAuthenticatedSource = true;
                        progress?.Report(new OperationProgress(0,
                            $"{source.Name} 需要登录 unmod.online，已跳过。"));
                        continue;
                    }
                }

                progress?.Report(new OperationProgress(0, $"正在连接 {source.Name}…"));
                using var client = _clientFactory(source.Timeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
                if (source.RequiresCommunityAuth)
                {
                    // 令牌只通过请求 Cookie 发送，绝不写入进度信息、异常文本或日志。
                    request.Headers.TryAddWithoutValidation("Cookie", $"token={communityToken}");
                }

                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                await WriteResponseAsync(response, destination, progress, token);
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                lastError = ex;
                if (index < sources.Count - 1)
                    progress?.Report(new OperationProgress(0, $"{source.Name} 连接失败，正在切换备用源…"));
            }
        }
        if (lastError is null && skippedAuthenticatedSource)
            throw new HttpRequestException("下载源需要登录社区账户，且没有可用的匿名备用源。");
        throw new HttpRequestException($"所有下载源均不可用：{lastError?.Message}", lastError);
    }

    private static async Task WriteResponseAsync(
        HttpResponseMessage response,
        string destination,
        IProgress<OperationProgress>? progress,
        CancellationToken token)
    {
        var total = response.Content.Headers.ContentLength ?? -1;
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, count), token);
            received += count;
            if (total > 0)
            {
                var percentage = (int)Math.Clamp(received * 100 / total, 0, 100);
                progress?.Report(new OperationProgress(percentage, $"正在下载：{percentage}%"));
            }
        }
    }
}
