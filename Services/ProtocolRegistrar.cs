using Microsoft.Win32;
using System.IO;

namespace UnturnedModManager.Services;

/// <summary>
/// Registers UMM's own <c>umm://</c> URI scheme. It deliberately does not claim UML's
/// <c>unmod://</c> scheme, so both launchers can coexist on the same Windows account.
/// </summary>
public static class ProtocolRegistrar
{
    public const string Scheme = "umm";
    private const string ProtocolName = "Unturned Mod Manager";

    public static void EnsureRegistered()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable)
                || !File.Exists(executable)
                || Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                return;

            using var key = Registry.CurrentUser.CreateSubKey($"SOFTWARE\\Classes\\{Scheme}");
            key.SetValue(null, $"URL:{ProtocolName}");
            key.SetValue("URL Protocol", string.Empty);

            using var commandKey = key.CreateSubKey("shell\\open\\command");
            var command = $"\"{executable}\" \"%1\"";
            if (!string.Equals(commandKey.GetValue(null) as string, command, StringComparison.OrdinalIgnoreCase))
                commandKey.SetValue(null, command);
        }
        catch
        {
            // Protocol support is optional; normal browsing and installation must remain available.
        }
    }

    /// <summary>Parses <c>umm://install/{id}</c>, <c>umm:install/{id}</c> or a raw positive id.</summary>
    public static bool TryParseInstallIntent(string? argument, out int modId)
    {
        modId = 0;
        if (string.IsNullOrWhiteSpace(argument))
            return false;

        var raw = argument.Trim();
        if (int.TryParse(raw, out var rawId) && rawId > 0)
        {
            modId = rawId;
            return true;
        }

        if (raw.StartsWith($"{Scheme}://", StringComparison.OrdinalIgnoreCase))
            raw = raw[(Scheme.Length + 3)..];
        else if (raw.StartsWith($"{Scheme}:", StringComparison.OrdinalIgnoreCase))
            raw = raw[(Scheme.Length + 1)..];
        else
            return false;

        raw = raw.TrimStart('/');
        var separator = raw.IndexOf('/');
        var action = separator < 0 ? raw : raw[..separator];
        var remainder = separator < 0 ? string.Empty : raw[(separator + 1)..];
        if (!action.Equals("install", StringComparison.OrdinalIgnoreCase))
            return false;

        var queryIndex = remainder.IndexOf('?');
        if (queryIndex >= 0)
            remainder = remainder[..queryIndex];

        return int.TryParse(remainder.Trim('/'), out modId) && modId > 0;
    }

    public static int? FindInstallIntent(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (string.Equals(argument, "--install", StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryParseInstallIntent(argument, out var modId))
                return modId;
        }

        return null;
    }
}
