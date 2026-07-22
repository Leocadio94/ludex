using System.IO;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib.Detectors;

/// <summary>
/// Reads Steam's own metadata: the install root from the registry, the library list from
/// libraryfolders.vdf, and one appmanifest_*.acf per installed game.
/// </summary>
public static class SteamDetector
{
    /// <summary>Steam entries that are runtimes or redistributables, not games.</summary>
    private static readonly HashSet<string> IgnoredAppIds = new(StringComparer.Ordinal)
    {
        "228980", // Steamworks Common Redistributables
        "1070560", // Steam Linux Runtime 1.0
        "1391110", // Steam Linux Runtime 2.0
        "1493710", // Proton Experimental
        "1628350" // Steam Linux Runtime 3.0
    };

    public static List<InstalledGame> Detect()
    {
        var root = GetSteamRoot();
        if (root is null) return new List<InstalledGame>();

        var games = new List<InstalledGame>();

        foreach (var library in GetLibraryPaths(root))
        {
            foreach (var (appId, name, installDir) in ReadAcfFiles(library))
            {
                if (IgnoredAppIds.Contains(appId)) continue;

                games.Add(new InstalledGame
                {
                    Name = name,
                    Store = GameStore.Steam,
                    StoreGameId = appId,
                    InstallPath = Path.Combine(library, "common", installDir),
                    // <root> for a Steam library is the folder that contains steamapps.
                    StoreRoot = Path.GetDirectoryName(library),
                    IconPath = FindLibraryArt(root, appId)
                });
            }
        }

        return games;
    }

    /// <summary>
    /// Steam caches library artwork per app under appcache\librarycache\&lt;appid&gt;. The
    /// client icon is stored under its content hash, so it is identified by shape (40 hex
    /// characters) rather than by name; the logo and header are wide and only used as
    /// fallbacks.
    /// </summary>
    private static string? FindLibraryArt(string steamRoot, string appId)
    {
        var dir = Path.Combine(steamRoot, "appcache", "librarycache", appId);
        if (!Directory.Exists(dir)) return null;

        try
        {
            foreach (var file in Directory.GetFiles(dir, "*.jpg"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length == 40 && name.All(Uri.IsHexDigit)) return file;
            }

            foreach (var fallback in new[] { "logo.png", "header.jpg" })
            {
                var path = Path.Combine(dir, fallback);
                if (File.Exists(path)) return path;
            }
        }
        catch
        {
            // Unreadable cache directory — fall back to the generic icon.
        }

        return null;
    }

    private static string? GetSteamRoot()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam"))
        {
            if (key?.GetValue("SteamPath") is string p && p.Length > 0)
            {
                var normalized = p.Replace('/', '\\');
                if (Directory.Exists(normalized)) return normalized;
            }
        }

        foreach (var p in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" })
        {
            if (Directory.Exists(p)) return p;
        }

        return null;
    }

    private static List<string> GetLibraryPaths(string steamRoot)
    {
        var fallback = new List<string> { Path.Combine(steamRoot, "steamapps") };
        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return fallback;

        try
        {
            var vdf = Vdf.Parse(File.ReadAllText(vdfPath));
            var paths = new List<string>();

            foreach (var value in vdf.Values)
            {
                if (value is Dictionary<string, object> entry &&
                    entry.TryGetValue("path", out var raw) &&
                    raw is string libraryRoot)
                {
                    paths.Add(Path.Combine(libraryRoot, "steamapps"));
                }
            }

            return paths.Count > 0 ? paths : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static List<(string AppId, string Name, string InstallDir)> ReadAcfFiles(string steamAppsPath)
    {
        var results = new List<(string, string, string)>();
        if (!Directory.Exists(steamAppsPath)) return results;

        string[] entries;
        try
        {
            entries = Directory.GetFiles(steamAppsPath, "appmanifest_*.acf");
        }
        catch
        {
            return results;
        }

        foreach (var file in entries)
        {
            try
            {
                var data = Vdf.Parse(File.ReadAllText(file));
                if (data.TryGetValue("appid", out var appid) && appid is string a &&
                    data.TryGetValue("name", out var name) && name is string n &&
                    data.TryGetValue("installdir", out var dir) && dir is string d)
                {
                    results.Add((a, n, d));
                }
            }
            catch
            {
                // Malformed manifest — skip this game rather than fail the whole store.
            }
        }

        return results;
    }
}
