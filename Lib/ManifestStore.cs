using System.IO;
using System.Text.Json;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib;

/// <summary>
/// Downloads the Ludusavi manifest and turns it into a compact on-disk index.
///
/// The manifest is ~16 MB of YAML describing ~19k games. It is parsed line by line
/// against its indentation instead of with a YAML library: the structure is fixed and
/// shallow, and a full parse would allocate far more than the data we actually keep.
/// Entries are written to the index one at a time, so the whole document is never
/// materialized.
/// </summary>
public sealed class ManifestStore
{
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/mtkennerly/ludusavi-manifest/master/data/manifest.yaml";

    private readonly string _manifestFile;
    private readonly string _indexFile;
    private readonly Settings _settings;
    private readonly Func<string, string, CancellationToken, Task> _download;
    private readonly Action _persistSettings;

    /// <param name="download">url, destination, token. Injected so the plugin can use Flow's
    /// HttpDownloadAsync (proxy aware) while tests can use a plain HttpClient.</param>
    public ManifestStore(
        string cacheDir,
        Settings settings,
        Func<string, string, CancellationToken, Task> download,
        Action persistSettings)
    {
        Directory.CreateDirectory(cacheDir);
        _manifestFile = Path.Combine(cacheDir, "manifest.yaml");
        _indexFile = Path.Combine(cacheDir, "manifest-index.json");
        _settings = settings;
        _download = download;
        _persistSettings = persistSettings;
    }

    public DateTime LastUpdatedUtc =>
        _settings.ManifestUpdatedUtcTicks == 0
            ? DateTime.MinValue
            : new DateTime(_settings.ManifestUpdatedUtcTicks, DateTimeKind.Utc);

    private bool IsFresh() =>
        DateTime.UtcNow - LastUpdatedUtc < TimeSpan.FromDays(_settings.ManifestTtlDays);

    public async Task<Dictionary<string, IndexedGame>> LoadAsync(
        bool force,
        Action<string>? onStatus = null,
        CancellationToken token = default)
    {
        if (!force && IsFresh() && File.Exists(_indexFile))
        {
            onStatus?.Invoke("Loading game database...");
            return await ReadIndexAsync(token).ConfigureAwait(false);
        }

        if (force || !File.Exists(_manifestFile) || !IsFresh())
        {
            onStatus?.Invoke("Downloading game database...");
            await _download(ManifestUrl, _manifestFile, token).ConfigureAwait(false);
        }

        onStatus?.Invoke("Indexing game database...");
        var count = await Task.Run(() => BuildIndex(onStatus, token), token).ConfigureAwait(false);
        onStatus?.Invoke($"Indexed {count:N0} games");

        _settings.ManifestUpdatedUtcTicks = DateTime.UtcNow.Ticks;
        _persistSettings();

        return await ReadIndexAsync(token).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, IndexedGame>> ReadIndexAsync(CancellationToken token)
    {
        await using var stream = File.OpenRead(_indexFile);
        var index = await JsonSerializer.DeserializeAsync(
            stream,
            JsonContext.Default.DictionaryStringIndexedGame,
            token).ConfigureAwait(false);
        return index ?? new Dictionary<string, IndexedGame>();
    }

    private int BuildIndex(Action<string>? onStatus, CancellationToken token)
    {
        var tempFile = _indexFile + ".tmp";
        var written = 0;

        using (var output = File.Create(tempFile))
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();

            var entryLines = new List<string>();
            var seen = 0;

            void Flush()
            {
                if (entryLines.Count == 0) return;

                var gameName = ExtractKey(entryLines[0].Trim());
                if (gameName.Length > 0)
                {
                    var indexed = ParseEntry(entryLines);
                    if (indexed is not null)
                    {
                        writer.WritePropertyName(gameName);
                        JsonSerializer.Serialize(writer, indexed, JsonContext.Default.IndexedGame);
                        written++;
                    }
                }

                seen++;
                if (seen % 2000 == 0)
                {
                    onStatus?.Invoke($"Indexing game database... ({seen:N0} games)");
                    writer.Flush();
                }

                entryLines.Clear();
            }

            foreach (var line in File.ReadLines(_manifestFile))
            {
                token.ThrowIfCancellationRequested();

                // Every root-level line is a game name; everything else belongs to it.
                if (line.Length > 0 && line[0] != ' ' && line[0] != '\t')
                {
                    Flush();
                    entryLines.Add(line);
                }
                else if (entryLines.Count > 0)
                {
                    entryLines.Add(line);
                }
            }

            Flush();

            writer.WriteEndObject();
            writer.Flush();
        }

        File.Move(tempFile, _indexFile, overwrite: true);
        return written;
    }

    /// <summary>Reads a YAML mapping key, handling both quoted and bare forms.</summary>
    private static string ExtractKey(string content)
    {
        if (content.StartsWith('"'))
        {
            var i = 1;
            while (i < content.Length && content[i] != '"')
            {
                if (content[i] == '\\') i++;
                i++;
            }
            return content[1..Math.Min(i, content.Length)].Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        return content.EndsWith(':') ? content[..^1].Trim() : content;
    }

    private enum Section
    {
        Other,
        Files,
        Steam,
        Gog
    }

    private enum FileSection
    {
        Other,
        Tags,
        When
    }

    /// <summary>
    /// Parses one game entry. Returns null when the game has no usable save/config path
    /// for this OS, which is the case for a large share of the manifest.
    /// </summary>
    private static IndexedGame? ParseEntry(List<string> entryLines)
    {
        var files = new List<IndexedFile>();
        int? steamId = null;
        int? gogId = null;
        string? alias = null;

        var section = Section.Other;
        var fileSection = FileSection.Other;

        string? filePath = null;
        var fileTags = PathTags.None;
        var fileWhen = new List<(string? Os, string? Store)>();

        void FlushFile()
        {
            if (filePath is not null && fileTags != PathTags.None)
            {
                if (fileWhen.Count == 0)
                {
                    files.Add(new IndexedFile { Template = filePath, Tags = fileTags });
                }
                else
                {
                    foreach (var (os, store) in fileWhen)
                    {
                        // Drop entries that only apply to another OS — halves the index.
                        if (os is not null && os != PathResolver.CurrentOs) continue;
                        files.Add(new IndexedFile
                        {
                            Template = filePath,
                            Tags = fileTags,
                            Os = os,
                            Store = store
                        });
                    }
                }
            }

            filePath = null;
            fileTags = PathTags.None;
            fileWhen.Clear();
            fileSection = FileSection.Other;
        }

        // Index 0 is the game name line, already consumed by the caller.
        for (var i = 1; i < entryLines.Count; i++)
        {
            var line = entryLines[i].TrimEnd('\r');
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] == '#') continue;

            var indent = line.Length - trimmed.Length;

            switch (indent)
            {
                case 2:
                    FlushFile();
                    section = trimmed switch
                    {
                        "files:" => Section.Files,
                        "steam:" => Section.Steam,
                        "gog:" => Section.Gog,
                        _ => Section.Other
                    };

                    if (section == Section.Other && trimmed.StartsWith("alias:", StringComparison.Ordinal))
                    {
                        var value = trimmed[6..].Trim();
                        if (value.Length > 0) alias = ExtractKey(value);
                    }
                    break;

                case 4:
                    if (section == Section.Files)
                    {
                        FlushFile();
                        filePath = ExtractKey(trimmed);
                    }
                    else if (section is Section.Steam or Section.Gog)
                    {
                        if (trimmed.StartsWith("id:", StringComparison.Ordinal) &&
                            int.TryParse(trimmed[3..].Trim(), out var id))
                        {
                            if (section == Section.Steam) steamId = id;
                            else gogId = id;
                        }
                    }
                    break;

                case 6:
                    if (section == Section.Files && filePath is not null)
                    {
                        fileSection = trimmed switch
                        {
                            "tags:" => FileSection.Tags,
                            "when:" => FileSection.When,
                            _ => FileSection.Other
                        };
                    }
                    break;

                case 8:
                    if (section != Section.Files || filePath is null) break;

                    if (fileSection == FileSection.Tags && trimmed.StartsWith("- ", StringComparison.Ordinal))
                    {
                        fileTags |= trimmed[2..] switch
                        {
                            "save" => PathTags.Save,
                            "config" => PathTags.Config,
                            _ => PathTags.None
                        };
                    }
                    else if (fileSection == FileSection.When && trimmed.StartsWith("- ", StringComparison.Ordinal))
                    {
                        var rest = trimmed[2..];
                        fileWhen.Add((ReadField(rest, "os:"), ReadField(rest, "store:")));
                    }
                    break;

                case 10:
                    // Continuation of the previous "when" list item, e.g. `store:` on its own line.
                    if (section == Section.Files && filePath is not null &&
                        fileSection == FileSection.When && fileWhen.Count > 0)
                    {
                        var (os, store) = fileWhen[^1];
                        fileWhen[^1] = (ReadField(trimmed, "os:") ?? os, ReadField(trimmed, "store:") ?? store);
                    }
                    break;
            }
        }

        FlushFile();

        // Alias-only entries carry no files but are still worth keeping: they let a game
        // detected under an older or regional name resolve to the canonical entry.
        if (files.Count == 0 && alias is null) return null;
        return new IndexedGame { SteamId = steamId, GogId = gogId, Files = files, Alias = alias };
    }

    /// <summary>Reads `key: value` out of a flow-style or block-style fragment.</summary>
    private static string? ReadField(string text, string key)
    {
        var idx = text.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return null;

        var start = idx + key.Length;
        while (start < text.Length && text[start] == ' ') start++;

        var end = start;
        while (end < text.Length && text[end] is not (' ' or ',' or '}' or '\r')) end++;

        return end > start ? text[start..end] : null;
    }
}
