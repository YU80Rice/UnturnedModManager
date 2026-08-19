using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace UnturnedModManager.Services;

/// <summary>
/// 从本项目的 GitHub Release 读取更新元数据，并在用户明确确认后下载、校验和安排替换。
/// 不轮询、不后台下载，也不会在用户确认前修改启动器文件。
/// </summary>
public sealed class LauncherUpdateService : IDisposable
{
    private const string RepositoryApiUrl = "https://api.github.com/repos/YU80Rice/UnturnedModManager/releases/latest";
    private const string RepositoryDownloadPrefix = "https://github.com/YU80Rice/UnturnedModManager/releases/download/";
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly string _downloadDirectory;

    public LauncherUpdateService()
        : this(null, null)
    {
    }

    public LauncherUpdateService(HttpClient? http, string? downloadDirectory)
    {
        _ownsClient = http is null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _downloadDirectory = downloadDirectory ?? Path.Combine(AppDataPaths.RootDirectory, "updates");
    }

    public async Task<LauncherUpdateInfo?> CheckForUpdateAsync(Version currentVersion, CancellationToken token = default)
    {
        var result = await CheckLatestReleaseAsync(currentVersion, token);
        return result.AvailableUpdate;
    }

    /// <summary>
    /// 一次读取最新 Release 的版本、说明和可选更新包。即使当前程序已是最新版本，
    /// 首页也可据此展示与 GitHub Release 一致的更新摘要；不会下载任何资产。
    /// </summary>
    public async Task<LauncherReleaseCheckResult> CheckLatestReleaseAsync(
        Version currentVersion,
        CancellationToken token = default)
    {
        var release = await GetLatestReleaseAsync(token);
        if (!TryParseReleaseVersion(release.TagName, out var latestVersion))
            return new LauncherReleaseCheckResult(null, null);

        var notes = new LauncherReleaseNotesInfo(
            latestVersion,
            release.Name?.Trim() ?? $"UMM v{latestVersion.ToString(3)}",
            release.Body?.Trim() ?? "",
            release.PublishedAt);

        if (latestVersion <= NormalizeVersion(currentVersion))
            return new LauncherReleaseCheckResult(notes, null);

        var assetName = $"UnturnedModManager-v{latestVersion.ToString(3)}-win-x64.exe";
        var asset = release.Assets.FirstOrDefault(item =>
            string.Equals(item.Name, assetName, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
            return new LauncherReleaseCheckResult(notes, null);

        if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var assetUri)
            || !assetUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !assetUri.AbsoluteUri.StartsWith(RepositoryDownloadPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Release 资产下载地址不属于 UMM 官方 GitHub Release。");

        var expectedSha256 = NormalizeSha256Digest(asset.Digest);
        if (expectedSha256 is null)
            throw new InvalidDataException("Release 未提供 SHA-256 摘要，已拒绝下载未校验更新。");

        if (asset.Size <= 0)
            throw new InvalidDataException("Release 资产大小无效。");

        var update = new LauncherUpdateInfo(
            latestVersion,
            notes.ReleaseName,
            notes.ReleaseNotes,
            asset.Name,
            assetUri,
            asset.Size,
            expectedSha256,
            release.PublishedAt);
        return new LauncherReleaseCheckResult(notes, update);
    }

    public async Task<string> DownloadAsync(
        LauncherUpdateInfo update,
        IProgress<OperationProgress>? progress = null,
        CancellationToken token = default)
    {
        var fileName = Path.GetFileName(update.AssetName);
        if (!string.Equals(fileName, update.AssetName, StringComparison.Ordinal)
            || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("更新文件名无效。");

        if (!update.DownloadUrl.AbsoluteUri.StartsWith(RepositoryDownloadPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("更新下载地址不属于 UMM 官方 GitHub Release。");

        Directory.CreateDirectory(_downloadDirectory);
        var destination = Path.Combine(_downloadDirectory, fileName);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".part";
        progress?.Report(new OperationProgress(0, "正在从 GitHub Release 下载启动器更新…"));

        try
        {
            using var request = CreateRequest(HttpMethod.Get, update.DownloadUrl.AbsoluteUri);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long received = 0;
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[80 * 1024];
                int count;
                while ((count = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    hash.AppendData(buffer, 0, count);
                    received += count;
                    var percentage = update.Size > 0
                        ? (int)Math.Clamp(received * 100 / update.Size, 0, 100)
                        : 0;
                    progress?.Report(new OperationProgress(percentage, $"正在下载 UMM 更新：{received / 1024d / 1024d:F1} / {update.Size / 1024d / 1024d:F1} MB"));
                }

                await output.FlushAsync(token);
            }

            if (received != update.Size)
                throw new InvalidDataException($"更新文件大小不匹配：期望 {update.Size} 字节，实际 {received} 字节。");

            var actualSha256 = Convert.ToHexString(hash.GetHashAndReset());
            if (!FixedTimeEquals(actualSha256, update.Sha256))
                throw new InvalidDataException("更新文件 SHA-256 校验失败，已拒绝安装。");

            File.Move(temporary, destination, overwrite: true);
            progress?.Report(new OperationProgress(100, "更新下载并校验完成。"));
            return destination;
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    /// <summary>
    /// 由一个短生命周期的 cmd 帮助器等待当前进程退出后替换 EXE；替换失败时不会删除旧版本，
    /// 而是直接启动已校验的下载文件，避免把用户留在不可启动状态。
    /// </summary>
    public static void ScheduleInstallAndRestart(string downloadedLauncher)
    {
        var source = Path.GetFullPath(downloadedLauncher);
        if (!File.Exists(source))
            throw new FileNotFoundException("未找到已校验的更新文件。", source);

        var target = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("无法确定当前启动器 EXE，已取消替换。\n你仍可手动运行下载的更新文件。");

        target = Path.GetFullPath(target);
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("更新文件正是当前运行的启动器，无法在运行中替换。请关闭启动器后手动运行该文件。");

        var scriptPath = Path.Combine(Path.GetDirectoryName(source)!, $"install-{Guid.NewGuid():N}.cmd");
        var script = $"""
@echo off
setlocal DisableDelayedExpansion
set "SOURCE={EscapeForBatch(source)}"
set "TARGET={EscapeForBatch(target)}"
set "BACKUP={EscapeForBatch(target + ".bak")}"
set "TARGET_PID={Environment.ProcessId}"
:wait_for_launcher
tasklist /fi "PID eq %TARGET_PID%" /nh | find "%TARGET_PID%" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait_for_launcher
)
if exist "%TARGET%" copy /y "%TARGET%" "%BACKUP%" >nul
copy /y "%SOURCE%" "%TARGET%" >nul
if errorlevel 1 (
    start "" "%SOURCE%"
) else (
    del /f /q "%SOURCE%" >nul 2>&1
    start "" "%TARGET%"
)
del "%~f0"
""";

        File.WriteAllText(scriptPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c \"\"{scriptPath}\"\"",
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("无法启动更新替换程序。");
    }

    public static bool TryParseReleaseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];
        if (!Version.TryParse(trimmed, out var parsed))
            return false;

        version = NormalizeVersion(parsed);
        return true;
    }

    public static string? NormalizeSha256Digest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
            return null;

        var value = digest.Trim();
        const string prefix = "sha256:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            value = value[prefix.Length..];
        return value.Length == 64 && value.All(Uri.IsHexDigit)
            ? value.ToUpperInvariant()
            : null;
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }

    private async Task<GitHubRelease> GetLatestReleaseAsync(CancellationToken token)
    {
        using var request = CreateRequest(HttpMethod.Get, RepositoryApiUrl);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: token)
            ?? throw new InvalidDataException("GitHub Release 返回为空。");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd("UnturnedModManager/2.1 (+https://github.com/YU80Rice/UnturnedModManager)");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        return request;
    }

    private static Version NormalizeVersion(Version version) => new(
        Math.Max(0, version.Major),
        Math.Max(0, version.Minor),
        Math.Max(0, version.Build));

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(actual);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string EscapeForBatch(string value) => value.Replace("%", "%%", StringComparison.Ordinal);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("published_at")] public DateTimeOffset? PublishedAt { get; set; }
        [JsonPropertyName("assets")] public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}

public sealed record LauncherUpdateInfo(
    Version Version,
    string ReleaseName,
    string ReleaseNotes,
    string AssetName,
    Uri DownloadUrl,
    long Size,
    string Sha256,
    DateTimeOffset? PublishedAt)
{
    public string DisplayVersion => "v" + Version.ToString(3);
}

public sealed record LauncherReleaseNotesInfo(
    Version Version,
    string ReleaseName,
    string ReleaseNotes,
    DateTimeOffset? PublishedAt)
{
    public string DisplayVersion => "v" + Version.ToString(3);
}

public sealed record LauncherReleaseCheckResult(
    LauncherReleaseNotesInfo? LatestRelease,
    LauncherUpdateInfo? AvailableUpdate);
