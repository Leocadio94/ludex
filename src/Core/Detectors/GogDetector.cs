using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace Ludex.Core.Detectors;

/// <summary>
/// GOG Galaxy registers every installed game under HKLM. Reading the registry directly
/// replaces the PowerShell round-trip the Raycast version needs.
/// </summary>
public static class GogDetector
{
    private static readonly string[] RootKeys =
    {
        @"SOFTWARE\WOW6432Node\GOG.com\Games",
        @"SOFTWARE\GOG.com\Games"
    };

    public static List<InstalledGame> Detect()
    {
        var games = new List<InstalledGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rootPath in RootKeys)
        {
            using var root = Registry.LocalMachine.OpenSubKey(rootPath);
            if (root is null) continue;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(subKeyName);
                if (key is null) continue;

                var name = key.GetValue("gameName") as string;
                var path = key.GetValue("path") as string;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;
                if (!seen.Add(path)) continue;

                var id = key.GetValue("gameId") as string
                         ?? key.GetValue("productId") as string
                         ?? subKeyName;

                games.Add(new InstalledGame
                {
                    Name = name,
                    Store = GameStore.Gog,
                    StoreGameId = string.IsNullOrEmpty(id) ? null : id,
                    InstallPath = path,
                    IconPath = FindGogIcon(path),
                    LaunchTarget = FindPrimaryPlayTask(path)
                });
            }
        }

        return games;
    }

    /// <summary>
    /// goggame-&lt;productid&gt;.info lists the game's play tasks. Launching the primary
    /// executable directly works whether or not GOG Galaxy is installed or running.
    /// </summary>
    private static string? FindPrimaryPlayTask(string installPath)
    {
        try
        {
            var info = Directory.Exists(installPath)
                ? Directory.GetFiles(installPath, "goggame-*.info").FirstOrDefault()
                : null;
            if (info is null) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(info));
            if (!doc.RootElement.TryGetProperty("playTasks", out var tasks)) return null;

            string? firstFileTask = null;

            foreach (var task in tasks.EnumerateArray())
            {
                if (!task.TryGetProperty("type", out var type) || type.GetString() != "FileTask") continue;
                if (!task.TryGetProperty("path", out var pathEl)) continue;
                if (pathEl.GetString() is not { Length: > 0 } relative) continue;

                var exe = Path.Combine(installPath, relative.Replace('/', '\\'));
                if (!File.Exists(exe)) continue;

                var isPrimary = task.TryGetProperty("isPrimary", out var primary) &&
                                primary.ValueKind == JsonValueKind.True;
                if (isPrimary) return exe;

                firstFileTask ??= exe;
            }

            return firstFileTask;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>GOG installers drop a goggame-&lt;productid&gt;.ico next to the game.</summary>
    private static string? FindGogIcon(string installPath)
    {
        try
        {
            return Directory.Exists(installPath)
                ? Directory.GetFiles(installPath, "goggame-*.ico").FirstOrDefault()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
