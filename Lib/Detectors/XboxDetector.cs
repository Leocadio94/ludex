using System.IO;
using System.Xml.Linq;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib.Detectors;

/// <summary>
/// Xbox / Game Pass detection from two cheap sources, merged by name:
///
///  1. The AppModel package repository in the registry, narrowed to packages that carry a
///     MicrosoftGame.config — that file only exists for GDK/Xbox titles, so it cuts ~570
///     installed packages down to the handful that are actually games.
///  2. The &lt;drive&gt;:\XboxGames folders, which is where the Xbox app puts game content
///     and therefore what the manifest's game-relative templates need.
///
/// The Raycast version shells out to Get-AppxPackage with a 25 s timeout; this runs in
/// well under a second and needs no PowerShell.
/// </summary>
public static class XboxDetector
{
    private const string PackagesKey =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    public static List<InstalledGame> Detect()
    {
        var byName = new Dictionary<string, InstalledGame>(StringComparer.Ordinal);

        foreach (var game in FromRegistry())
            byName.TryAdd(Matcher.NormalizeName(game.Name), game);

        foreach (var (name, contentPath) in FromXboxGamesFolders())
        {
            var key = Matcher.NormalizeName(name);
            if (byName.TryGetValue(key, out var existing))
            {
                // The registry points at the WindowsApps stub; the real content lives here.
                existing.InstallPath = contentPath;
            }
            else
            {
                byName[key] = new InstalledGame
                {
                    Name = name,
                    Store = GameStore.Xbox,
                    InstallPath = contentPath
                };
            }
        }

        return byName.Values.ToList();
    }

    private static IEnumerable<InstalledGame> FromRegistry()
    {
        using var root = Registry.CurrentUser.OpenSubKey(PackagesKey);
        if (root is null) yield break;

        foreach (var subKeyName in root.GetSubKeyNames())
        {
            InstalledGame? game = null;

            try
            {
                using var key = root.OpenSubKey(subKeyName);
                if (key?.GetValue("PackageRootFolder") is not string packageRoot || packageRoot.Length == 0)
                    continue;

                // Only GDK/Xbox titles ship this file.
                if (!File.Exists(Path.Combine(packageRoot, "MicrosoftGame.config"))) continue;

                var name = key.GetValue("DisplayName") as string;
                if (string.IsNullOrEmpty(name) || name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                    name = ReadManifestDisplayName(packageRoot);
                if (string.IsNullOrEmpty(name)) continue;

                game = new InstalledGame
                {
                    Name = name,
                    Store = GameStore.Xbox,
                    InstallPath = packageRoot
                };
            }
            catch
            {
                // Unreadable package key — skip it.
            }

            if (game is not null) yield return game;
        }
    }

    private static string? ReadManifestDisplayName(string packageRoot)
    {
        var manifest = Path.Combine(packageRoot, "AppxManifest.xml");
        if (!File.Exists(manifest)) return null;

        try
        {
            var doc = XDocument.Load(manifest);
            var name = doc.Root?
                .Elements().FirstOrDefault(e => e.Name.LocalName == "Properties")?
                .Elements().FirstOrDefault(e => e.Name.LocalName == "DisplayName")?
                .Value;

            return string.IsNullOrEmpty(name) || name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)
                ? null
                : name;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<(string Name, string ContentPath)> FromXboxGamesFolders()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;

            var xboxGames = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
            if (!Directory.Exists(xboxGames)) continue;

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(xboxGames);
            }
            catch
            {
                continue;
            }

            foreach (var dir in dirs)
            {
                var name = Path.GetFileName(dir);

                // Not a game — this is where the Xbox app keeps cloud save containers.
                if (name.Equals("GameSave", StringComparison.OrdinalIgnoreCase)) continue;

                var content = Path.Combine(dir, "Content");
                yield return (name, Directory.Exists(content) ? content : dir);
            }
        }
    }
}
