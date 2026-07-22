using System.IO;
using System.Windows.Controls;
using Flow.Launcher.Plugin.GameSavesManager.Lib;

namespace Flow.Launcher.Plugin.GameSavesManager;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider, IAsyncReloadable
{
    private const string DrillPrefix = ">";

    private PluginInitContext _context = null!;
    private Settings _settings = null!;
    private ManifestStore _manifest = null!;
    private GameDetector _detector = null!;

    private readonly object _gate = new();
    private Task? _loading;

    private volatile bool _ready;
    private string _status = "Starting...";
    private string? _error;

    private Dictionary<string, IndexedGame> _index = new();
    private List<GameWithPaths> _games = new();
    private Dictionary<string, GameWithPaths> _bySlug = new(StringComparer.Ordinal);

    #region Lifecycle

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();

        var cacheDir = context.CurrentPluginMetadata.PluginCacheDirectoryPath;

        _manifest = new ManifestStore(
            cacheDir,
            _settings,
            (url, dest, token) => context.API.HttpDownloadAsync(url, dest, token: token),
            () => context.API.SaveSettingJsonStorage<Settings>());

        _detector = new GameDetector(
            cacheDir,
            _settings,
            (message, e) => context.API.LogException(nameof(Main), message, e));

        // Loading the manifest can take seconds on a cold start; InitAsync blocks Flow's
        // startup, so kick it off and let QueryAsync wait on it instead.
        StartLoad(false, false);
        return Task.CompletedTask;
    }

    public Task ReloadDataAsync() => StartLoad(false, true);

    private Task StartLoad(bool forceManifest, bool forceGames)
    {
        lock (_gate)
        {
            if (_loading is { IsCompleted: false }) return _loading;
            _loading = Task.Run(() => LoadCoreAsync(forceManifest, forceGames));
            return _loading;
        }
    }

    private async Task LoadCoreAsync(bool forceManifest, bool forceGames)
    {
        _ready = false;
        _error = null;

        try
        {
            _index = await _manifest.LoadAsync(forceManifest, s => _status = s).ConfigureAwait(false);

            _status = "Detecting installed games...";
            var installed = await _detector.DetectAsync(forceGames).ConfigureAwait(false);

            _status = "Matching against the database...";
            var games = Matcher.Match(installed, _index, _settings.ShowUnmatchedGames);

            _games = games;
            _bySlug = games.ToDictionary(g => g.Slug, StringComparer.Ordinal);
            _ready = true;
            _status = $"{games.Count} games";
        }
        catch (Exception e)
        {
            _error = e.Message;
            _context.API.LogException(nameof(Main), "Failed to load game data", e);
        }
    }

    #endregion

    #region Query

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var search = query.Search.Trim();

        if (!_ready)
        {
            // Give the background load a chance to finish so the common case needs no
            // second keystroke; fall back to a status line when it is still downloading.
            var loading = StartLoad(false, false);
            await Task.WhenAny(loading, Task.Delay(2500, CancellationToken.None)).ConfigureAwait(false);

            if (!_ready) return StatusResults();
        }

        if (search.StartsWith(DrillPrefix, StringComparison.Ordinal))
            return DrillInResults(query, search[DrillPrefix.Length..]);

        if (search.StartsWith('!'))
            return CommandResults(query, search[1..]);

        return GameResults(query, search);
    }

    private List<Result> StatusResults()
    {
        if (_error is not null)
        {
            return Single("Failed to load game data", _error, Icons.Missing, _ =>
            {
                StartLoad(true, true);
                _context.API.ShowMsg("Game Saves Manager", "Retrying...");
                return false;
            });
        }

        return Single("Loading game database...", _status, Icons.App, _ =>
        {
            _context.API.ReQuery();
            return false;
        });
    }

    private List<Result> GameResults(Query query, string search)
    {
        var results = new List<Result>();

        foreach (var game in _games)
        {
            var score = 100;

            if (search.Length > 0)
            {
                var match = _context.API.FuzzySearch(search, game.Name);
                if (!match.Success && !game.DetectedName.Equals(game.Name, StringComparison.OrdinalIgnoreCase))
                    match = _context.API.FuzzySearch(search, game.DetectedName);
                if (!match.Success) continue;

                score = match.Score;
                results.Add(GameResult(query, game, score, match.MatchData));
                continue;
            }

            results.Add(GameResult(query, game, score, null));
        }

        results = results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Take(_settings.MaxResults)
            .ToList();

        if (search.Length == 0) results.AddRange(CommandHints(query));

        return results;
    }

    private Result GameResult(Query query, GameWithPaths game, int score, IList<int>? highlight)
    {
        var counts = new List<string>();
        if (game.SavePaths.Count > 0) counts.Add($"{game.SavePaths.Count} save{(game.SavePaths.Count > 1 ? "s" : "")}");
        if (game.ConfigPaths.Count > 0) counts.Add($"{game.ConfigPaths.Count} config{(game.ConfigPaths.Count > 1 ? "s" : "")}");
        if (counts.Count == 0) counts.Add("no known save paths");

        var subtitle = $"{StoreLabel(game.Store)}  ·  {string.Join(", ", counts)}";
        if (!game.Name.Equals(game.DetectedName, StringComparison.OrdinalIgnoreCase))
            subtitle += $"  ·  installed as \"{game.DetectedName}\"";

        return new Result
        {
            Title = game.Name,
            SubTitle = subtitle,
            TitleHighlightData = highlight,
            IcoPath = game.Matched ? Icons.App : Icons.Missing,
            Score = score,
            ContextData = game,
            AutoCompleteText = $"{ActionKeyword(query)} {game.Name}",
            Action = _ =>
            {
                _context.API.ChangeQuery($"{ActionKeyword(query)} {DrillPrefix}{game.Slug} ");
                return false;
            }
        };
    }

    private List<Result> DrillInResults(Query query, string rest)
    {
        var space = rest.IndexOf(' ');
        var slug = space < 0 ? rest : rest[..space];
        var filter = space < 0 ? "" : rest[(space + 1)..].Trim();

        if (!_bySlug.TryGetValue(slug, out var game))
            return Single("Game not found", "It may have been removed since the last refresh.", Icons.Missing, _ =>
            {
                _context.API.ChangeQuery($"{ActionKeyword(query)} ");
                return false;
            });

        var results = new List<Result>
        {
            new()
            {
                Title = "Back to all games",
                SubTitle = game.Name,
                IcoPath = Icons.Back,
                Score = 1000,
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{ActionKeyword(query)} ");
                    return false;
                }
            }
        };

        var score = 900;
        foreach (var entry in BuildEntries(game))
        {
            if (filter.Length > 0 && !_context.API.FuzzySearch(filter, entry.Title).Success) continue;
            results.Add(FolderResult(entry, score--));
        }

        if (results.Count == 1)
            results.Add(NoPathsResult(game));

        return results;
    }

    private List<Result> CommandResults(Query query, string command)
    {
        if (command.StartsWith("all", StringComparison.OrdinalIgnoreCase))
            return SearchAllResults(query, command[3..].Trim());

        var results = new List<Result>();

        foreach (var hint in CommandHints(query))
        {
            if (command.Length == 0 || _context.API.FuzzySearch(command, hint.Title).Success)
                results.Add(hint);
        }

        return results;
    }

    private List<Result> CommandHints(Query query)
    {
        var keyword = ActionKeyword(query);

        return new List<Result>
        {
            new()
            {
                Title = "Refresh Installed Games",
                SubTitle = $"{keyword} !games  ·  re-scan Steam, GOG, Epic and Xbox",
                IcoPath = Icons.Refresh,
                Score = 3,
                Action = _ =>
                {
                    RunRefresh(false, true, "Re-detecting installed games...");
                    return false;
                }
            },
            new()
            {
                Title = "Refresh Game Database",
                SubTitle = $"{keyword} !refresh  ·  re-download the Ludusavi manifest (~16 MB)",
                IcoPath = Icons.Download,
                Score = 2,
                Action = _ =>
                {
                    RunRefresh(true, true, "Downloading the game database...");
                    return false;
                }
            },
            new()
            {
                Title = "Search All Games",
                SubTitle = $"{keyword} !all <name>  ·  search the whole database, not just installed games",
                IcoPath = Icons.Search,
                Score = 1,
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{keyword} !all ");
                    return false;
                }
            }
        };
    }

    private void RunRefresh(bool manifest, bool games, string message)
    {
        _ready = false;
        _status = message;
        _context.API.ShowMsg("Game Saves Manager", message, Icons.App);

        var task = StartLoad(manifest, games);
        _ = task.ContinueWith(_ =>
        {
            _context.API.ShowMsg(
                "Game Saves Manager",
                _error is null ? $"Ready — {_games.Count} games" : $"Failed: {_error}",
                Icons.App);
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Searches the whole manifest, including games that are not installed. Paths that
    /// need an install location cannot be resolved here and are dropped by the resolver.
    /// </summary>
    private List<Result> SearchAllResults(Query query, string search)
    {
        if (search.Length < 2)
            return Single("Search all games", "Type at least two characters.", Icons.Search, _ => false);

        var scored = new List<(int Score, string Name, IList<int> Highlight)>();

        foreach (var (name, entry) in _index)
        {
            // Alias-only entries would show up as duplicates of their canonical entry.
            if (entry.Files.Count == 0) continue;

            var match = _context.API.FuzzySearch(search, name);
            if (match.Success) scored.Add((match.Score, name, match.MatchData));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(_settings.MaxResults)
            .Select(s =>
            {
                var game = ManifestOnlyGame(s.Name);
                return new Result
                {
                    Title = s.Name,
                    SubTitle = $"Database entry  ·  {game.SavePaths.Count} save, {game.ConfigPaths.Count} config paths",
                    TitleHighlightData = s.Highlight,
                    IcoPath = Icons.Search,
                    Score = s.Score,
                    ContextData = game,
                    Action = _ =>
                    {
                        _bySlug[game.Slug] = game;
                        _context.API.ChangeQuery($"{ActionKeyword(query)} {DrillPrefix}{game.Slug} ");
                        return false;
                    }
                };
            })
            .ToList();
    }

    /// <summary>Builds a GameWithPaths for a manifest entry with no local installation.</summary>
    private GameWithPaths ManifestOnlyGame(string manifestName)
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

    #endregion

    #region Folder entries

    private sealed record FolderEntry(string Title, string Path, bool Exists, string Icon);

    private static IEnumerable<FolderEntry> BuildEntries(GameWithPaths game)
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in game.SavePaths)
        {
            if (!emitted.Add(p.Path)) continue;
            yield return new FolderEntry(FolderTitle(p, PathTags.Save), p.Path, p.Exists,
                p.Exists ? Icons.Folder : Icons.Missing);
        }

        foreach (var p in game.ConfigPaths)
        {
            if (!emitted.Add(p.Path)) continue;
            yield return new FolderEntry(FolderTitle(p, PathTags.Config), p.Path, p.Exists,
                p.Exists ? Icons.Config : Icons.Missing);
        }

        if (!string.IsNullOrEmpty(game.InstallPath))
        {
            var exists = Directory.Exists(game.InstallPath);
            yield return new FolderEntry("Open Install Folder", game.InstallPath, exists,
                exists ? Icons.Install : Icons.Missing);
        }
    }

    private static string FolderTitle(ResolvedPath p, PathTags context)
    {
        var hasBoth = p.Tags.HasFlag(PathTags.Save) && p.Tags.HasFlag(PathTags.Config);
        var name = hasBoth
            ? "Save & Config Folder"
            : context == PathTags.Save ? "Save Folder" : "Config Folder";

        var parts = new List<string>();
        if (p.Os is not null) parts.Add(Capitalize(p.Os));
        if (p.Store is not null) parts.Add(Capitalize(p.Store));

        return parts.Count > 0 ? $"Open {name} ({string.Join(", ", parts)})" : $"Open {name}";
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private Result FolderResult(FolderEntry entry, int score) => new()
    {
        Title = entry.Title,
        SubTitle = entry.Exists ? entry.Path : $"{entry.Path}   (not created yet)",
        SubTitleToolTip = entry.Path,
        IcoPath = entry.Icon,
        Score = score,
        ContextData = entry,
        Action = _ => OpenFolder(entry.Path)
    };

    private Result NoPathsResult(GameWithPaths game) => new()
    {
        Title = "No known save or config paths",
        SubTitle = $"{game.Name} has no usable entry in the Ludusavi manifest.",
        IcoPath = Icons.Missing,
        Score = 500,
        Action = _ => false
    };

    private bool OpenFolder(string path)
    {
        var target = TargetDirectory(path);

        if (!Directory.Exists(target))
        {
            _context.API.ShowMsgError("Folder does not exist", target);
            return false;
        }

        _context.API.OpenDirectory(target);
        return true;
    }

    /// <summary>
    /// The manifest points at files as often as at folders. Opening the parent is what the
    /// user wants either way.
    /// </summary>
    private static string TargetDirectory(string path)
    {
        if (Directory.Exists(path)) return path;
        if (File.Exists(path)) return Path.GetDirectoryName(path) ?? path;
        return Path.HasExtension(path) ? Path.GetDirectoryName(path) ?? path : path;
    }

    #endregion

    #region Context menu

    public List<Result> LoadContextMenus(Result selectedResult)
    {
        switch (selectedResult.ContextData)
        {
            case GameWithPaths game:
            {
                var results = new List<Result>();
                var score = 100;
                foreach (var entry in BuildEntries(game)) results.Add(FolderResult(entry, score--));
                if (results.Count == 0) results.Add(NoPathsResult(game));
                return results;
            }

            case FolderEntry entry:
                return new List<Result>
                {
                    new()
                    {
                        Title = "Copy Path",
                        SubTitle = entry.Path,
                        IcoPath = Icons.Copy,
                        Score = 100,
                        Action = _ =>
                        {
                            _context.API.CopyToClipboard(entry.Path);
                            return true;
                        }
                    },
                    new()
                    {
                        Title = "Open Folder",
                        SubTitle = entry.Path,
                        IcoPath = Icons.Folder,
                        Score = 90,
                        Action = _ => OpenFolder(entry.Path)
                    }
                };

            default:
                return new List<Result>();
        }
    }

    #endregion

    #region Helpers

    public Control CreateSettingPanel() => new SettingsPanel(_settings, _context.API);

    private string ActionKeyword(Query query) =>
        string.IsNullOrEmpty(query.ActionKeyword) || query.ActionKeyword == Query.GlobalPluginWildcardSign
            ? _context.CurrentPluginMetadata.ActionKeyword
            : query.ActionKeyword;

    private static string StoreLabel(GameStore store) => store switch
    {
        GameStore.Steam => "Steam",
        GameStore.Gog => "GOG",
        GameStore.Epic => "Epic Games",
        GameStore.Xbox => "Xbox",
        _ => store.ToString()
    };

    private static List<Result> Single(string title, string subtitle, string icon, Func<ActionContext, bool> action) =>
        new()
        {
            new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = icon,
                Score = 100,
                Action = action
            }
        };

    #endregion
}

internal static class Icons
{
    public const string App = @"Images\icon.png";
    public const string Folder = @"Images\folder.png";
    public const string Config = @"Images\config.png";
    public const string Install = @"Images\install.png";
    public const string Missing = @"Images\missing.png";
    public const string Refresh = @"Images\refresh.png";
    public const string Download = @"Images\download.png";
    public const string Search = @"Images\search.png";
    public const string Copy = @"Images\copy.png";
    public const string Back = @"Images\back.png";
}
