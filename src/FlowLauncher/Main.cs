using System.IO;
using System.Windows.Controls;
using Ludex.Core;

namespace Flow.Launcher.Plugin.Ludex;

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
            (url, dest, report, token) => context.API.HttpDownloadAsync(url, dest, report, token),
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

    private Task StartLoad(bool forceManifest, bool forceGames, Action<double>? onProgress = null,
        CancellationToken token = default)
    {
        lock (_gate)
        {
            if (_loading is { IsCompleted: false }) return _loading;
            _loading = Task.Run(() => LoadCoreAsync(forceManifest, forceGames, onProgress, token), token);
            return _loading;
        }
    }

    private async Task LoadCoreAsync(bool forceManifest, bool forceGames, Action<double>? onProgress,
        CancellationToken token)
    {
        _ready = false;
        _error = null;

        try
        {
            _index = await _manifest.LoadAsync(forceManifest, s => _status = s, onProgress, token)
                .ConfigureAwait(false);

            _status = "Detecting installed games...";
            onProgress?.Invoke(85);
            var installed = await _detector.DetectAsync(forceGames, token).ConfigureAwait(false);

            _status = "Matching against the database...";
            onProgress?.Invoke(95);
            var games = Matcher.Match(installed, _index, _settings.ShowUnmatchedGames);

            _games = games;
            _bySlug = games.ToDictionary(g => g.Slug, StringComparer.Ordinal);
            _ready = true;
            _status = $"{games.Count} games";
            onProgress?.Invoke(100);
        }
        catch (OperationCanceledException)
        {
            _status = "Cancelled";
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
                _context.API.ShowMsg("Ludex", "Retrying...");
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

        if (search.Length > 0)
        {
            foreach (var game in _games)
            {
                var match = _context.API.FuzzySearch(search, game.Name);
                if (!match.Success && !game.DetectedName.Equals(game.Name, StringComparison.OrdinalIgnoreCase))
                    match = _context.API.FuzzySearch(search, game.DetectedName);
                if (!match.Success) continue;

                results.Add(GameResult(query, game, match.Score, match.MatchData, forceOrder: false));
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .Take(_settings.MaxResults)
                .ToList();
        }

        // Empty query: the full installed list. In Alphabetical mode every game gets a
        // strictly descending score by A-Z position and opts out of Flow's usage bump, so the
        // order is fixed. In Usage mode (default) every game shares one score and keeps the
        // bump, letting Flow float the games you open most to the top.
        var alphabetical = _settings.DefaultSort == GameSort.Alphabetical;
        var ordered = alphabetical
            ? _games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            : _games.AsEnumerable();

        var rank = _games.Count;
        foreach (var game in ordered.Take(_settings.MaxResults))
        {
            var score = alphabetical ? rank-- : 100;
            results.Add(GameResult(query, game, score, null, forceOrder: alphabetical));
        }

        results.AddRange(CommandHints(query));
        return results;
    }

    private Result GameResult(Query query, GameWithPaths game, int score, IList<int>? highlight,
        bool forceOrder)
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
            IcoPath = GameIcon(game),
            Score = score,
            // In Alphabetical mode the score already encodes the order, so keep Flow's usage
            // history from re-sorting it.
            AddSelectedCount = !forceOrder,
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

        var launch = LaunchResult(game, 950);
        if (launch is not null &&
            (filter.Length == 0 || _context.API.FuzzySearch(filter, launch.Title).Success))
            results.Add(launch);

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
                Action = ctx =>
                {
                    _ = RunRefreshAsync(false, true, "Re-detecting installed games...");
                    return false;
                }
            },
            new()
            {
                Title = "Refresh Game Database",
                SubTitle = $"{keyword} !refresh  ·  re-download the Ludusavi manifest (~16 MB)",
                IcoPath = Icons.Download,
                Score = 2,
                Action = ctx =>
                {
                    _ = RunRefreshAsync(true, true, "Downloading the game database...");
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

    /// <summary>
    /// Runs a refresh behind Flow's progress dialog. Called straight from a result action so
    /// the dialog is created on the UI thread; the returned task is intentionally not awaited.
    /// </summary>
    private async Task RunRefreshAsync(bool manifest, bool games, string caption)
    {
        _ready = false;
        _status = caption;

        using var cts = new CancellationTokenSource();

        try
        {
            await _context.API.ShowProgressBoxAsync(
                caption,
                async report =>
                {
                    report(0);
                    await StartLoad(manifest, games, report, cts.Token).ConfigureAwait(false);
                },
                cts.Cancel);
        }
        catch (Exception e)
        {
            _context.API.LogException(nameof(Main), "Refresh failed", e);
        }

        if (cts.IsCancellationRequested) return;

        _context.API.ShowMsg(
            "Ludex",
            _error is null ? $"Ready — {_games.Count} games" : $"Failed: {_error}",
            Icons.App);
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

    private sealed class FolderEntry
    {
        public required string Title { get; set; }

        /// <summary>The directory Enter will open.</summary>
        public required string Path { get; init; }

        public required bool Exists { get; init; }
        public required string Icon { get; init; }

        /// <summary>The manifest paths that resolved here — one folder can come from several.</summary>
        public List<string> Sources { get; } = new();
    }

    /// <summary>
    /// Builds the folder list for a game, grouped by the directory that will actually be
    /// opened. The manifest often lists several files inside one folder (Crimson Desert has
    /// three save entries in the same directory), and opening a file means opening its parent,
    /// so without this the list shows the same destination several times over.
    /// </summary>
    private static List<FolderEntry> BuildEntries(GameWithPaths game)
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
            var exists = Directory.Exists(dir);
            var entry = new FolderEntry
            {
                Title = FolderTitle(group.Tags, group.Os, group.Store),
                Path = dir,
                Exists = exists,
                Icon = exists
                    ? group.Tags == PathTags.Config ? Icons.Config : Icons.Folder
                    : Icons.Missing
            };

            entry.Sources.AddRange(group.Sources);
            entries.Add(entry);
        }

        if (!string.IsNullOrEmpty(game.InstallPath))
        {
            var exists = Directory.Exists(game.InstallPath);
            entries.Add(new FolderEntry
            {
                Title = "Open Install Folder",
                Path = game.InstallPath,
                Exists = exists,
                Icon = exists ? Icons.Install : Icons.Missing
            });
        }

        Disambiguate(entries);
        return entries;
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
        var name = tags.HasFlag(PathTags.Save) && tags.HasFlag(PathTags.Config)
            ? "Save & Config Folder"
            : tags.HasFlag(PathTags.Save) ? "Save Folder" : "Config Folder";

        var parts = new List<string>();
        if (os is not null) parts.Add(Capitalize(os));
        if (store is not null) parts.Add(Capitalize(store));

        return parts.Count > 0 ? $"Open {name} ({string.Join(", ", parts)})" : $"Open {name}";
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private Result FolderResult(FolderEntry entry, int score)
    {
        var target = TargetDirectory(entry.Path);
        var fallback = Directory.Exists(target) ? null : NearestExistingAncestor(target);

        // Say up front what Enter will actually do, rather than letting the user press it and
        // watch nothing happen.
        var subtitle = fallback is null
            ? entry.Path
            : $"{entry.Path}   —   not created yet, opens {fallback}";

        var tooltip = entry.Sources.Count > 1
            ? $"{entry.Path}\n\nFrom:\n{string.Join('\n', entry.Sources)}"
            : entry.Path;

        return new Result
        {
            Title = entry.Title,
            SubTitle = subtitle,
            SubTitleToolTip = tooltip,
            IcoPath = entry.Icon,
            Score = score,
            ContextData = entry,
            Action = _ => OpenFolder(entry.Path)
        };
    }

    /// <summary>Null when the store gave the detector no way to start the game.</summary>
    private Result? LaunchResult(GameWithPaths game, int score)
    {
        if (game.LaunchTarget is not { Length: > 0 } target) return null;

        return new Result
        {
            Title = "Launch Game",
            SubTitle = GameLauncher.Describe(game.Store, target),
            IcoPath = GameIcon(game),
            Score = score,
            Action = _ =>
            {
                try
                {
                    GameLauncher.Launch(target, game.InstallPath);
                    return true;
                }
                catch (Exception e)
                {
                    _context.API.LogException(nameof(Main), $"Failed to launch {game.Name}", e);
                    _context.API.ShowMsgError("Could not launch the game", e.Message);
                    return false;
                }
            }
        };
    }

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

        if (Directory.Exists(target))
        {
            _context.API.OpenDirectory(target);
            return true;
        }

        // A save folder that does not exist yet is the normal case for a game that has never
        // been launched. Opening the closest existing parent is more useful than an error,
        // and it puts the user where the folder will appear.
        var ancestor = NearestExistingAncestor(target);
        if (ancestor is null)
        {
            _context.API.ShowMsgError("Folder does not exist", target);
            return false;
        }

        _context.API.OpenDirectory(ancestor);
        _context.API.ShowMsg("Folder not created yet", $"Opened {ancestor} instead.", Icons.Missing);
        return true;
    }

    private static string? NearestExistingAncestor(string path)
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

                var launch = LaunchResult(game, 150);
                if (launch is not null) results.Add(launch);

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

    /// <summary>
    /// Store artwork when the detector found any, otherwise the plugin icon — or the
    /// question mark when the game has no manifest entry at all.
    /// </summary>
    private static string GameIcon(GameWithPaths game) =>
        game.IconPath is { Length: > 0 } icon ? icon : game.Matched ? Icons.App : Icons.Missing;

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
