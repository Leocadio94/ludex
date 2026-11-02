using System.IO;

namespace Ludex.Core;

/// <summary>One row of a game's folder list: the directory Enter will actually open.</summary>
public sealed class FolderEntry
{
    public required string Title { get; set; }

    /// <summary>The directory Enter will open.</summary>
    public required string Path { get; init; }

    public required bool Exists { get; init; }
    public PathTags Tags { get; init; }
    public bool IsInstall { get; init; }

    /// <summary>The manifest paths that resolved here — one folder can come from several.</summary>
    public List<string> Sources { get; } = new();
}

public static class FolderEntries
{
    /// <summary>
    /// Builds the folder list for a game, grouped by the directory that will actually be
    /// opened. The manifest often lists several files inside one folder (Crimson Desert has
    /// three save entries in the same directory), and opening a file means opening its parent,
    /// so without this the list shows the same destination several times over.
    /// </summary>
    public static List<FolderEntry> Build(GameWithPaths game)
    {
        var groups = new Dictionary<string, (PathTags Tags, string? Os, string? Store, List<string> Sources)>(
            StringComparer.OrdinalIgnoreCase);

        var distinct = game.SavePaths
            .Concat(game.ConfigPaths)
            .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var p in distinct)
        {
            var dir = TargetDirectory(p.Path);

            if (groups.TryGetValue(dir, out var existing))
            {
                groups[dir] = (existing.Tags | p.Tags, existing.Os ?? p.Os, existing.Store ?? p.Store,
                    existing.Sources);
            }
            else
            {
                groups[dir] = (p.Tags, p.Os, p.Store, new List<string>());
            }

            groups[dir].Sources.Add(p.Path);
        }

        var entries = new List<FolderEntry>();

        foreach (var (dir, group) in groups)
        {
            var entry = new FolderEntry
            {
                Title = FolderTitle(group.Tags, group.Os, group.Store),
                Path = dir,
                Exists = Directory.Exists(dir),
                Tags = group.Tags
            };

            entry.Sources.AddRange(group.Sources);
            entries.Add(entry);
        }

        if (!string.IsNullOrEmpty(game.InstallPath))
        {
            entries.Add(new FolderEntry
            {
                Title = Texts.T("Open Install Folder"),
                Path = game.InstallPath,
                Exists = Directory.Exists(game.InstallPath),
                IsInstall = true
            });
        }

        Disambiguate(entries);
        return entries;
    }

    /// <summary>
    /// The manifest points at files as often as at folders. Opening the parent is what the
    /// user wants either way.
    /// </summary>
    public static string TargetDirectory(string path)
    {
        if (Directory.Exists(path)) return path;
        if (File.Exists(path)) return Path.GetDirectoryName(path) ?? path;
        return Path.HasExtension(path) ? Path.GetDirectoryName(path) ?? path : path;
    }

    public static string? NearestExistingAncestor(string path)
    {
        var current = Path.GetDirectoryName(path);

        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current)) return current;
            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    /// <summary>
    /// Two folders can legitimately produce the same title — a per-account save directory and
    /// the shared one above it, for instance. Append the folder name so the rows differ by
    /// something other than a truncated path.
    /// </summary>
    private static void Disambiguate(List<FolderEntry> entries)
    {
        foreach (var collision in entries.GroupBy(e => e.Title).Where(g => g.Count() > 1))
        {
            foreach (var entry in collision)
            {
                var leaf = Path.GetFileName(entry.Path);
                if (leaf.Length > 0) entry.Title = $"{entry.Title} — {leaf}";
            }
        }
    }

    private static string FolderTitle(PathTags tags, string? os, string? store)
    {
        var name = Texts.T(tags.HasFlag(PathTags.Save) && tags.HasFlag(PathTags.Config)
            ? "Open Save & Config Folder"
            : tags.HasFlag(PathTags.Save) ? "Open Save Folder" : "Open Config Folder");

        var parts = new List<string>();
        if (os is not null) parts.Add(Capitalize(os));
        if (store is not null) parts.Add(Capitalize(store));

        return parts.Count > 0 ? $"{name} ({string.Join(", ", parts)})" : name;
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
