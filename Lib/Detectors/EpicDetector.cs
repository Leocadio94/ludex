using System.IO;
using System.Text.Json;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib.Detectors;

/// <summary>
/// The Epic Games Launcher writes one JSON manifest per installed game into ProgramData.
/// </summary>
public static class EpicDetector
{
    public static List<InstalledGame> Detect()
    {
        var games = new List<InstalledGame>();

        var manifestsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");

        if (!Directory.Exists(manifestsDir)) return games;

        string[] files;
        try
        {
            files = Directory.GetFiles(manifestsDir, "*.item");
        }
        catch
        {
            return games;
        }

        foreach (var file in files)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;

                if (root.TryGetProperty("bIsIncompleteInstall", out var incomplete) &&
                    incomplete.ValueKind == JsonValueKind.True)
                    continue;

                if (!root.TryGetProperty("DisplayName", out var nameEl)) continue;
                if (!root.TryGetProperty("InstallLocation", out var pathEl)) continue;

                var name = nameEl.GetString();
                var installPath = pathEl.GetString();
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(installPath)) continue;

                var id = root.TryGetProperty("CatalogItemId", out var catalogEl)
                    ? catalogEl.GetString()
                    : root.TryGetProperty("AppName", out var appEl) ? appEl.GetString() : null;

                games.Add(new InstalledGame
                {
                    Name = name,
                    Store = GameStore.Epic,
                    StoreGameId = id,
                    InstallPath = installPath
                });
            }
            catch
            {
                // Malformed manifest — skip.
            }
        }

        return games;
    }
}
