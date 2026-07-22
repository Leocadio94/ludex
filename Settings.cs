namespace Flow.Launcher.Plugin.GameSavesManager;

public sealed class Settings
{
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
