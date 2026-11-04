using System.IO;
using System.Text;

namespace Ludex.Core;

/// <summary>
/// Joins detected games to manifest entries and resolves their path templates.
/// </summary>
public static class Matcher
{
    /// <summary>Store names as they appear in the manifest's `when.store` field.</summary>
    private static readonly Dictionary<GameStore, string[]> StoreAliases = new()
    {
        [GameStore.Steam] = new[] { "steam" },
        [GameStore.Gog] = new[] { "gog", "gogGalaxy" },
        [GameStore.Epic] = new[] { "epic" },
        [GameStore.Xbox] = new[] { "microsoft", "xbox" }
    };

    public static string NormalizeName(string name)
    {
        var sb = new StringBuilder(name.Length);
        var lastWasSpace = true;

        foreach (var ch in name)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
            }
            // Everything else (punctuation, ™, :) is dropped entirely.
        }

        return sb.ToString().TrimEnd();
    }

    public static List<GameWithPaths> Match(
        IReadOnlyList<InstalledGame> installedGames,
        Dictionary<string, IndexedGame> index,
        bool includeUnmatched)
    {
        var bySteamId = new Dictionary<int, string>();
        var byGogId = new Dictionary<int, string>();
        var byNormalizedName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (manifestName, entry) in index)
        {
            if (entry.SteamId is { } steamId) bySteamId[steamId] = manifestName;
            if (entry.GogId is { } gogId) byGogId[gogId] = manifestName;
            byNormalizedName[NormalizeName(manifestName)] = manifestName;
        }

        var results = new List<GameWithPaths>(installedGames.Count);
        var usedSlugs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var game in installedGames)
        {
            var manifestName = ResolveManifestName(game, bySteamId, byGogId, byNormalizedName);
            manifestName = FollowAlias(manifestName, index);

            var savePaths = new List<ResolvedPath>();
            var configPaths = new List<ResolvedPath>();

            // The alias target can be missing when the canonical entry had no Windows paths.
            if (manifestName is not null && index.TryGetValue(manifestName, out var entry))
            {
                var aliases = StoreAliases.GetValueOrDefault(game.Store, Array.Empty<string>());

                foreach (var file in entry.Files)
                {
                    if (file.Os is not null && file.Os != PathResolver.CurrentOs) continue;
                    if (file.Store is not null && !aliases.Contains(file.Store, StringComparer.OrdinalIgnoreCase)) continue;

                    foreach (var resolved in PathResolver.ResolveAndExpand(file.Template, game.InstallPath, game.StoreRoot, game.StoreGameId))
                    {
                        var rp = new ResolvedPath
                        {
                            Path = resolved,
                            Exists = PathExists(resolved),
                            Tags = file.Tags,
                            Os = file.Os,
                            Store = file.Store
                        };

                        if (file.Tags.HasFlag(PathTags.Save)) savePaths.Add(rp);
                        if (file.Tags.HasFlag(PathTags.Config)) configPaths.Add(rp);
                    }
                }
            }

            var matched = manifestName is not null && (savePaths.Count > 0 || configPaths.Count > 0);
            if (!matched && !includeUnmatched) continue;
            if (!matched && string.IsNullOrEmpty(game.InstallPath)) continue;

            results.Add(new GameWithPaths
            {
                Slug = MakeSlug(game, usedSlugs),
                Name = manifestName ?? game.Name,
                DetectedName = game.Name,
                Matched = matched,
                Store = game.Store,
                StoreGameId = game.StoreGameId,
                InstallPath = game.InstallPath,
                IconPath = game.IconPath,
                LaunchTarget = game.LaunchTarget,
                SavePaths = Deduplicate(savePaths),
                ConfigPaths = Deduplicate(configPaths)
            });
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    /// <summary>
    /// Resolves an alias entry to the entry that actually holds the paths. Alias chains are
    /// not supposed to nest, but the hop limit keeps a bad manifest from looping forever.
    /// </summary>
    public static string? FollowAlias(string? name, Dictionary<string, IndexedGame> index)
    {
        for (var hop = 0; name is not null && hop < 4; hop++)
        {
            if (!index.TryGetValue(name, out var entry) || entry.Alias is null) return name;
            name = entry.Alias;
        }

        return name;
    }

    private static string? ResolveManifestName(
        InstalledGame game,
        Dictionary<int, string> bySteamId,
        Dictionary<int, string> byGogId,
        Dictionary<string, string> byNormalizedName)
    {
        // Store IDs are authoritative; names are the fallback.
        if (game.StoreGameId is not null && int.TryParse(game.StoreGameId, out var numericId))
        {
            if (game.Store == GameStore.Steam && bySteamId.TryGetValue(numericId, out var steamName))
                return steamName;
            if (game.Store == GameStore.Gog && byGogId.TryGetValue(numericId, out var gogName))
                return gogName;
        }

        return byNormalizedName.GetValueOrDefault(NormalizeName(game.Name));
    }

    private static bool PathExists(string p)
    {
        try
        {
            return Directory.Exists(p) || File.Exists(p);
        }
        catch
        {
            return false;
        }
    }

    private static List<ResolvedPath> Deduplicate(List<ResolvedPath> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return paths.Where(p => seen.Add(p.Path)).ToList();
    }

    /// <summary>
    /// Space-free identifier used by the drill-in query. Uniqueness matters more than
    /// readability — two Steam libraries can hold the same game name.
    /// </summary>
    private static string MakeSlug(InstalledGame game, HashSet<string> used)
    {
        var store = game.Store.ToString().ToLowerInvariant();
        var id = string.IsNullOrEmpty(game.StoreGameId)
            ? NormalizeName(game.Name).Replace(' ', '_')
            : game.StoreGameId;

        if (id.Length == 0) id = "unknown";

        var slug = $"{store}-{id}";
        var candidate = slug;
        var n = 2;
        while (!used.Add(candidate)) candidate = $"{slug}~{n++}";

        return candidate;
    }
}
