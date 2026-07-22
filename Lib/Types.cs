using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib;

public enum GameStore
{
    Steam,
    Gog,
    Epic,
    Xbox
}

[Flags]
public enum PathTags
{
    None = 0,
    Save = 1,
    Config = 2
}

/// <summary>A game detected on this machine by one of the store detectors.</summary>
public sealed class InstalledGame
{
    public string Name { get; set; } = string.Empty;
    public GameStore Store { get; set; }

    /// <summary>Steam appid, GOG productId, Epic catalog item id.</summary>
    public string? StoreGameId { get; set; }

    public string? InstallPath { get; set; }

    /// <summary>
    /// The manifest's &lt;root&gt;: the store root this game was installed under, i.e. the
    /// folder for which &lt;base&gt; == &lt;root&gt;/steamapps/common/&lt;game&gt;. Null falls back
    /// to the install directory's parent.
    /// </summary>
    public string? StoreRoot { get; set; }

    /// <summary>
    /// Store-provided artwork or an executable Flow can extract an icon from. Resolved once
    /// during detection and cached, since it means touching the disk per game.
    /// </summary>
    public string? IconPath { get; set; }
}

/// <summary>
/// One save/config path template from the manifest, already flattened to a single
/// when-condition. Property names are shortened because ~19k games worth of these
/// are serialized to the on-disk index.
/// </summary>
public sealed class IndexedFile
{
    [JsonPropertyName("t")] public string Template { get; set; } = string.Empty;
    [JsonPropertyName("g")] public PathTags Tags { get; set; }

    /// <summary>null = any OS.</summary>
    [JsonPropertyName("o")] public string? Os { get; set; }

    /// <summary>null = any store. Uses the manifest's store naming ("microsoft", not "xbox").</summary>
    [JsonPropertyName("s")] public string? Store { get; set; }
}

public sealed class IndexedGame
{
    [JsonPropertyName("s")] public int? SteamId { get; set; }
    [JsonPropertyName("g")] public int? GogId { get; set; }
    [JsonPropertyName("f")] public List<IndexedFile> Files { get; set; } = new();

    /// <summary>
    /// Set when this manifest key is just another name for a different entry (~1000 of
    /// them). Such entries carry no files of their own.
    /// </summary>
    [JsonPropertyName("a")] public string? Alias { get; set; }
}

/// <summary>A manifest template resolved to a real filesystem path.</summary>
public sealed class ResolvedPath
{
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public PathTags Tags { get; set; }
    public string? Os { get; set; }
    public string? Store { get; set; }
}

/// <summary>An installed game joined with whatever the manifest knows about it.</summary>
public sealed class GameWithPaths
{
    /// <summary>Stable, space-free identifier used for the drill-in query.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Manifest name when matched, otherwise the name reported by the store.</summary>
    public string Name { get; set; } = string.Empty;

    public string DetectedName { get; set; } = string.Empty;
    public bool Matched { get; set; }

    public GameStore Store { get; set; }
    public string? StoreGameId { get; set; }
    public string? InstallPath { get; set; }
    public string? IconPath { get; set; }

    public List<ResolvedPath> SavePaths { get; set; } = new();
    public List<ResolvedPath> ConfigPaths { get; set; } = new();
}
