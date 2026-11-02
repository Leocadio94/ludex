using System.IO;

namespace Ludex.Core;

/// <summary>
/// The loaded state both launchers read from: the manifest index, the installed games
/// matched against it, and whether a (re)load is in flight. Loading runs once in the
/// background; front ends poll <see cref="Ready"/> or subscribe to <see cref="Changed"/>.
/// </summary>
public sealed class GameCatalog
{
    private readonly ManifestStore _manifest;
    private readonly GameDetector _detector;
    private readonly Settings _settings;
    private readonly Action<string, Exception> _logError;

    private readonly object _gate = new();
    private Task? _loading;

    private volatile bool _ready;
    private volatile string _status = Texts.T("Starting...");
    private volatile string? _error;

    private Dictionary<string, IndexedGame> _index = new();
    private List<GameWithPaths> _games = new();
    private Dictionary<string, GameWithPaths> _bySlug = new(StringComparer.Ordinal);

    public GameCatalog(
        ManifestStore manifest,
        GameDetector detector,
        Settings settings,
        Action<string, Exception> logError)
    {
        _manifest = manifest;
        _detector = detector;
        _settings = settings;
        _logError = logError;
    }

    public bool Ready => _ready;
    public string Status => _status;
    public string? Error => _error;

    public Dictionary<string, IndexedGame> Index => _index;
    public List<GameWithPaths> Games => _games;

    /// <summary>Raised when a load finishes, successfully or not.</summary>
    public event Action? Changed;

    public Task StartLoad(bool forceManifest, bool forceGames, Action<double>? onProgress = null,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            if (_loading is { IsCompleted: false }) return _loading;
            _loading = Task.Run(() => LoadCoreAsync(forceManifest, forceGames, onProgress, token), token);
            return _loading;
        }
    }

    /// <summary>Marks the catalog as busy so front ends show a status line during a refresh.</summary>
    public void Begin(string status)
    {
        _ready = false;
        _status = status;
    }

    public bool TryGetGame(string slug, out GameWithPaths game) => _bySlug.TryGetValue(slug, out game!);

    /// <summary>Makes a game that is not installed reachable by its slug.</summary>
    public void Register(GameWithPaths game) => _bySlug[game.Slug] = game;

    private async Task LoadCoreAsync(bool forceManifest, bool forceGames, Action<double>? onProgress,
        CancellationToken token)
    {
        _ready = false;
        _error = null;

        try
        {
            _index = await _manifest.LoadAsync(forceManifest, s => _status = s, onProgress, token)
                .ConfigureAwait(false);

            _status = Texts.T("Detecting installed games...");
            onProgress?.Invoke(85);
            var installed = await _detector.DetectAsync(forceGames, token).ConfigureAwait(false);

            _status = Texts.T("Matching against the database...");
            onProgress?.Invoke(95);
            var games = Matcher.Match(installed, _index, _settings.ShowUnmatchedGames);

            _games = games;
            _bySlug = games.ToDictionary(g => g.Slug, StringComparer.Ordinal);
            _ready = true;
            _status = Texts.F("{0} games", games.Count);
            onProgress?.Invoke(100);
        }
        catch (OperationCanceledException)
        {
            _status = Texts.T("Cancelled");
        }
        catch (Exception e)
        {
            _error = e.Message;
            _logError("Failed to load game data", e);
        }

        Changed?.Invoke();
    }

    /// <summary>Builds a GameWithPaths for a manifest entry with no local installation.</summary>
    public GameWithPaths ManifestOnlyGame(string manifestName)
    {
        var entry = _index[manifestName];
        var game = new GameWithPaths
        {
            Slug = "db-" + Matcher.NormalizeName(manifestName).Replace(' ', '_'),
            Name = manifestName,
            DetectedName = manifestName,
            Matched = true
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in entry.Files)
        {
            if (file.Os is not null && file.Os != PathResolver.CurrentOs) continue;

            foreach (var resolved in PathResolver.ResolveAndExpand(file.Template, null))
            {
                if (!seen.Add(resolved)) continue;

                var rp = new ResolvedPath
                {
                    Path = resolved,
                    Exists = Directory.Exists(resolved) || File.Exists(resolved),
                    Tags = file.Tags,
                    Os = file.Os,
                    Store = file.Store
                };

                if (file.Tags.HasFlag(PathTags.Save)) game.SavePaths.Add(rp);
                if (file.Tags.HasFlag(PathTags.Config)) game.ConfigPaths.Add(rp);
            }
        }

        return game;
    }
}
