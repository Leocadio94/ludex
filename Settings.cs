using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.GameSavesManager;

public enum GameSort
{
    /// <summary>Flow's usage ranking: games you open often float to the top.</summary>
    Usage,

    /// <summary>Strict A-Z, ignoring how often each game is opened.</summary>
    Alphabetical
}

public sealed class Settings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GameSort DefaultSort { get; set; } = GameSort.Usage;

    public int ManifestTtlDays { get; set; } = 7;
    public int InstalledGamesTtlMinutes { get; set; } = 60;

    public bool DetectSteam { get; set; } = true;
    public bool DetectGog { get; set; } = true;
    public bool DetectEpic { get; set; } = true;
    public bool DetectXbox { get; set; } = true;

    /// <summary>Show installed games that have no Ludusavi manifest entry (install folder only).</summary>
    public bool ShowUnmatchedGames { get; set; } = true;

    public int MaxResults { get; set; } = 50;

    /// <summary>UTC ticks of the last successful manifest index build. 0 = never.</summary>
    public long ManifestUpdatedUtcTicks { get; set; }
}
