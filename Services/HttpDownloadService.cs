using System.IO;
using System.Net.Http;

namespace UnturnedModManager.Services;

public sealed record DownloadSource(string Name, string Url, TimeSpan Timeout);
public sealed record OperationProgress(int Percentage, string Message);

public sealed class HttpDownloadService
{
    public async Task DownloadAsync(
        IReadOnlyList<DownloadSource> sources,
        string destination,
        IProgress<OperationProgress>? progress = null,
        CancellationToken token = default)
    {
        Exception? lastError = null;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            try
            {
                progress?.Report(new OperationProgress(0, $"正在连接 {source.Name}…"));
                using var client = new HttpClient { Timeout = source.Timeout };
                using var response = await client.GetAsync(
                    source.Url, HttpCompletionOption.ResponseHeadersRead, token);
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
