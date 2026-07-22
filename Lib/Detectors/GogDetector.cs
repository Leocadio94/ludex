using System.IO;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib.Detectors;

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
                    IconPath = FindGogIcon(path)
                });
            }
        }

        return games;
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
