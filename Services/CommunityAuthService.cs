using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using UnturnedModManager.Models;

namespace UnturnedModManager.Services;

public sealed class CommunityAuthService : IDisposable
{
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public CommunityUser? CurrentUser { get; private set; }
    private bool _sessionValidated;
    public bool IsSignedIn => _sessionValidated && CurrentUser is not null && !string.IsNullOrWhiteSpace(AppSettings.CommunityAuthToken);
    public bool HasCachedUser => CurrentUser is not null;
    public bool IsSessionPending => HasCachedUser && !IsSignedIn && !string.IsNullOrWhiteSpace(AppSettings.CommunityAuthToken);
    public event Action? SessionChanged;

    public CommunityAuthService()
    {
        _http = new HttpClient(new HttpClientHandler { CookieContainer = _cookies, UseCookies = true })
        { BaseAddress = new Uri(CommunityApiClient.BaseUrl + "/"), Timeout = TimeSpan.FromSeconds(30) };
        RestoreToken();
    }

    public async Task<bool> RestoreAsync(CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(AppSettings.CommunityAuthToken))
        {
            CurrentUser = null;
            _sessionValidated = false;
            SessionChanged?.Invoke();
            return false;
        }
        try
        {
            var response = await _http.GetFromJsonAsync<MeResponse>("api/auth/me", JsonOptions, token);
            CurrentUser = response?.User;
            _sessionValidated = CurrentUser is not null;
            if (CurrentUser is not null) SaveUser(CurrentUser);
            SessionChanged?.Invoke();
            return CurrentUser is not null;
        }
        catch
        {
            // 网络暂时不可用时保留缓存账户，但不把它当作已验证登录。
            _sessionValidated = false;
            SessionChanged?.Invoke();
            return false;
        }
    }

    public void RestoreCachedUser()
    {
        if (!string.IsNullOrWhiteSpace(AppSettings.CommunityUsername))
            CurrentUser = new CommunityUser
            {
                Id = AppSettings.CommunityUserId ?? 0,
                Username = AppSettings.CommunityUsername,
                Role = AppSettings.CommunityRole ?? "",
                AvatarUrl = AppSettings.CommunityAvatarUrl
            };
        _sessionValidated = false;
        SessionChanged?.Invoke();
    }

    public async Task<(bool Success, string Message)> LoginViaBrowserAsync(CancellationToken token = default)
    {
        using var listener = new HttpListener();
        const int port = 52026;
        listener.Prefixes.Add($"http://localhost:{port}/");
        try { listener.Start(); }
        catch (Exception ex) { return (false, $"无法启动本地登录回调：{ex.Message}"); }
        try
        {
            var loginUrl = $"{CommunityApiClient.BaseUrl}/login?next=/api/auth/cli-login?port={port}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(loginUrl) { UseShellExecute = true });
            string? tokenValue = null;
            while (tokenValue is null)
            {
                var context = await listener.GetContextAsync().WaitAsync(token);
                if (!string.Equals(context.Request.Url?.AbsolutePath, "/callback", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }
                tokenValue = context.Request.QueryString["token"];
                var response = context.Response;
                response.StatusCode = string.IsNullOrWhiteSpace(tokenValue) ? 400 : 200;
                response.ContentType = "text/html; charset=utf-8";
                await using var writer = new StreamWriter(response.OutputStream);
                await writer.WriteAsync(string.IsNullOrWhiteSpace(tokenValue)
                    ? "<html><meta charset='utf-8'><body>登录失败，请返回启动器重试。</body></html>"
                    : "<html><meta charset='utf-8'><body>登录成功，可以关闭此页面并返回启动器。</body></html>");
                response.Close();
                if (string.IsNullOrWhiteSpace(tokenValue)) return (false, "人机验证或登录未完成");
            }
            SaveToken(tokenValue);
            var restored = await RestoreAsync(token);
            return restored
                ? (true, $"欢迎回来，{CurrentUser!.DisplayIdentity}")
                : (false, "登录成功，但无法读取账户信息");
        }
        catch (OperationCanceledException) { return (false, "登录已取消"); }
        catch (Exception ex) { return (false, ex.Message); }
        finally { listener.Stop(); }
    }

    public Task<(bool Success, string Message)> LoginAsync(string username, string password, CancellationToken token = default) =>
        Task.FromResult((false, "社区登录需要在人机验证页面中完成，请点击侧栏账户按钮在浏览器中登录。"));

    public async Task LogoutAsync(CancellationToken token = default)
    {
        try { await _http.PostAsync("api/auth/logout", null, token); } catch { }
        CurrentUser = null;
        _sessionValidated = false;
        AppSettings.CommunityAuthToken = null; AppSettings.CommunityUserId = null;
        AppSettings.CommunityUsername = null; AppSettings.CommunityRole = null; AppSettings.CommunityAvatarUrl = null;
        SessionChanged?.Invoke();
    }
    private void SaveToken(string value) { _sessionValidated = false; AppSettings.CommunityAuthToken = value; _cookies.SetCookies(new Uri(CommunityApiClient.BaseUrl), $"token={value}; path=/"); }
    private void RestoreToken() { if (!string.IsNullOrWhiteSpace(AppSettings.CommunityAuthToken)) _cookies.SetCookies(new Uri(CommunityApiClient.BaseUrl), $"token={AppSettings.CommunityAuthToken}; path=/"); }
    private static void SaveUser(CommunityUser user)
    {
        AppSettings.CommunityUserId = user.Id;
        AppSettings.CommunityUsername = user.Username;
        AppSettings.CommunityRole = user.Role;
        AppSettings.CommunityAvatarUrl = user.AvatarUrl;
    }
    public void Dispose() => _http.Dispose();
    private sealed class MeResponse { public CommunityUser? User { get; set; } }
}

public sealed class CommunityUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }

    /// <summary>只本地化服务端实际提供的 role，不根据昵称或本地插件推断特权身份。</summary>
    public string RoleDisplay => DescribeRole(Role);
    public string DisplayIdentity => string.IsNullOrWhiteSpace(Username)
        ? RoleDisplay
        : $"{RoleDisplay} · {Username}";

    public static string DescribeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "社区成员";

        var labels = role.Split([',', ';', '|', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant() switch
            {
                "owner" or "admin" or "administrator" => "社区管理员",
                "moderator" or "mod" => "社区管理员",
                "creator" or "author" or "developer" or "publisher" => "创作者",
                "user" or "member" => "社区成员",
                _ => value
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return labels.Length == 0 ? "社区成员" : string.Join(" · ", labels);
    }
}
