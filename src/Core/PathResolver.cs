using System.IO;
using System.Text.RegularExpressions;

namespace Ludex.Core;

/// <summary>
/// Turns Ludusavi manifest path templates into real filesystem paths.
/// Windows-only: Flow Launcher does not run anywhere else.
/// </summary>
public static partial class PathResolver
{
    public const string CurrentOs = "windows";

    [GeneratedRegex(@"<(?:game|root|base)>")]
    private static partial Regex GameRelativeRegex();

    [GeneratedRegex(@"<\w+>")]
    private static partial Regex AnyPlaceholderRegex();

    private const string StoreGameIdPlaceholder = "<storeGameId>";

    private static readonly Dictionary<string, string> Vars = BuildVars();

    private static Dictionary<string, string> BuildVars()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["<home>"] = home,
            ["<osUserName>"] = Environment.UserName,
            ["<winAppData>"] = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ["<winLocalAppData>"] = localAppData,
            ["<winLocalAppDataLow>"] = Path.Combine(home, "AppData", "LocalLow"),
            ["<winDocuments>"] = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ["<winPublic>"] = Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public",
            ["<winProgramData>"] = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ["<winDir>"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            // The manifest uses these for per-account subfolders; a wildcard lets
            // ExpandWildcard find whichever account actually exists on disk.
            ["<storeUserId>"] = "*"
        };
    }

    /// <summary>
    /// Substitutes every placeholder in <paramref name="template"/>.
    /// Returns null when the template cannot be fully resolved (e.g. a game-relative
    /// path for a game with no known install location, or a <c>&lt;storeGameId&gt;</c>
    /// path for a game with no known store ID).
    /// </summary>
    public static string? ResolveTemplate(string template, string? installPath, string? storeRoot = null, string? storeGameId = null)
    {
        var result = template;

        // A wildcard here would match every other game's folder (e.g. Steam userdata), so an unknown ID skips the path.
        if (result.Contains(StoreGameIdPlaceholder, StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(storeGameId)) return null;
            result = result.Replace(StoreGameIdPlaceholder, storeGameId, StringComparison.Ordinal);
        }

        if (GameRelativeRegex().IsMatch(result))
        {
            if (string.IsNullOrEmpty(installPath)) return null;

            var trimmed = installPath.TrimEnd('\\', '/');
            var gameName = Path.GetFileName(trimmed);

            // <root> is the store root (the folder holding steamapps for Steam), not the
            // game folder's parent — templates like <root>/userdata/<storeUserId>/... depend
            // on this. Detectors that cannot determine it fall back to the parent directory.
            var root = storeRoot ?? Path.GetDirectoryName(trimmed) ?? trimmed;

            result = result.Replace("<game>", gameName)
                           .Replace("<root>", root.TrimEnd('\\', '/'))
                           .Replace("<base>", trimmed);
        }

        foreach (var (placeholder, value) in Vars)
        {
            if (result.Contains(placeholder, StringComparison.Ordinal))
                result = result.Replace(placeholder, value, StringComparison.Ordinal);
        }

        // Anything left unresolved means we would open a bogus path — drop it.
        if (AnyPlaceholderRegex().IsMatch(result)) return null;

        return NormalizeSeparators(result);
    }

    private static string NormalizeSeparators(string p)
    {
        var s = p.Replace('/', '\\');
        while (s.Contains(@"\\", StringComparison.Ordinal) && !s.StartsWith(@"\\", StringComparison.Ordinal))
            s = s.Replace(@"\\", @"\", StringComparison.Ordinal);
        return s.TrimEnd('\\');
    }

    /// <summary>
    /// Expands '*' segments against the real filesystem. A path without wildcards is
    /// returned as-is even when it does not exist yet — the game may simply not have
    /// been launched, and showing the expected location is still useful.
    /// </summary>
    public static List<string> ExpandWildcard(string p)
    {
        if (!p.Contains('*')) return new List<string> { p };

        var results = new List<string>();
        Expand(p, results, 0);

        // A template like ".../SaveFolder/*.json" expands to nothing until the game has
        // actually written a save. The concrete parent is still the folder the user wants,
        // so fall back to it — but only when every wildcard sits in the final segment,
        // otherwise the fallback would be a shared root like Steam's "userdata".
        if (results.Count == 0)
        {
            var lastSep = p.LastIndexOf('\\');
            if (lastSep > 0)
            {
                var parent = p[..lastSep];
                if (!parent.Contains('*')) results.Add(parent);
            }
        }

        return results;
    }

    private static void Expand(string p, List<string> results, int depth)
    {
        // Manifest templates never nest this deep; the guard is against pathological
        // wildcard trees blowing the stack or the query budget.
        if (depth > 8 || results.Count > 64) return;

        var parts = p.Split('\\');
        var wildcardIdx = Array.FindIndex(parts, part => part.Contains('*'));
        if (wildcardIdx < 0)
        {
            results.Add(p);
            return;
        }

        var parent = string.Join('\\', parts[..wildcardIdx]);
        if (parent.Length == 0) return;
        if (parent.Length == 2 && parent[1] == ':') parent += '\\';
        if (!Directory.Exists(parent)) return;

        var pattern = parts[wildcardIdx];
        var remaining = parts[(wildcardIdx + 1)..];

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(parent, pattern);
        }
        catch
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (remaining.Length == 0)
                results.Add(entry);
            else
                Expand(Path.Combine(entry, string.Join('\\', remaining)), results, depth + 1);
        }
    }

    public static List<string> ResolveAndExpand(string template, string? installPath, string? storeRoot = null, string? storeGameId = null)
    {
        var resolved = ResolveTemplate(template, installPath, storeRoot, storeGameId);
        return resolved is null ? new List<string>() : ExpandWildcard(resolved);
    }
}
