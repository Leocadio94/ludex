using System.IO;
using System.Text.Json;
using Flow.Launcher.Plugin.GameSavesManager.Lib.Detectors;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib;

/// <summary>
/// Runs every enabled store detector in parallel and caches the result. A store that
/// throws is dropped silently rather than failing the whole detection.
/// </summary>
public sealed class GameDetector
{
    private readonly string _cacheFile;
    private readonly Settings _settings;
    private readonly Action<string, Exception> _logError;

    public GameDetector(string cacheDir, Settings settings, Action<string, Exception> logError)
    {
        Directory.CreateDirectory(cacheDir);
        // The suffix is a schema version: bumping it invalidates caches written by an older
        // build whose InstalledGame shape differed, instead of serving stale null fields.
        _cacheFile = Path.Combine(cacheDir, "installed-games-v3.json");
        _settings = settings;
        _logError = logError;
    }

    public async Task<List<InstalledGame>> DetectAsync(bool force, CancellationToken token = default)
    {
        if (!force)
        {
            var cached = ReadCache();
            if (cached is not null) return cached;
        }

        var tasks = new List<Task<List<InstalledGame>>>();
        if (_settings.DetectSteam) tasks.Add(RunAsync(SteamDetector.Detect, nameof(SteamDetector)));
        if (_settings.DetectGog) tasks.Add(RunAsync(GogDetector.Detect, nameof(GogDetector)));
        if (_settings.DetectEpic) tasks.Add(RunAsync(EpicDetector.Detect, nameof(EpicDetector)));
        if (_settings.DetectXbox) tasks.Add(RunAsync(XboxDetector.Detect, nameof(XboxDetector)));

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var games = results.SelectMany(r => r).ToList();

        WriteCache(games);
        return games;
    }

    private Task<List<InstalledGame>> RunAsync(Func<List<InstalledGame>> detect, string name) =>
        Task.Run(() =>
        {
            try
            {
                return detect();
            }
            catch (Exception e)
            {
                _logError($"{name} failed", e);
                return new List<InstalledGame>();
            }
        });

    private List<InstalledGame>? ReadCache()
    {
        try
        {
            if (!File.Exists(_cacheFile)) return null;

            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(_cacheFile);
            if (age > TimeSpan.FromMinutes(_settings.InstalledGamesTtlMinutes)) return null;

            using var stream = File.OpenRead(_cacheFile);
            return JsonSerializer.Deserialize(stream, JsonContext.Default.ListInstalledGame);
        }
        catch
        {
            return null;
        }
    }

    private void WriteCache(List<InstalledGame> games)
    {
        try
        {
            using var stream = File.Create(_cacheFile);
            JsonSerializer.Serialize(stream, games, JsonContext.Default.ListInstalledGame);
        }
        catch (Exception e)
        {
            _logError("Failed to write installed-games cache", e);
        }
    }
}
