using System.IO;
using System.Windows.Controls;
using Ludex.Core;

namespace Flow.Launcher.Plugin.Ludex;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider, IAsyncReloadable
{
    private const string DrillPrefix = ">";

    private PluginInitContext _context = null!;
    private Settings _settings = null!;
    private GameCatalog _catalog = null!;

    #region Lifecycle

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        _settings = context.API.LoadSettingJsonStorage<Settings>();

        var cacheDir = context.CurrentPluginMetadata.PluginCacheDirectoryPath;

        var manifest = new ManifestStore(
            cacheDir,
            _settings,
            (url, dest, report, token) => context.API.HttpDownloadAsync(url, dest, report, token),
            () => context.API.SaveSettingJsonStorage<Settings>());

        var logError = (string message, Exception e) => context.API.LogException(nameof(Main), message, e);
        var detector = new GameDetector(cacheDir, _settings, logError);

        _catalog = new GameCatalog(manifest, detector, _settings, logError);

        // Loading the manifest can take seconds on a cold start; InitAsync blocks Flow's
        // startup, so kick it off and let QueryAsync wait on it instead.
        _catalog.StartLoad(false, false);
        return Task.CompletedTask;
    }

    public Task ReloadDataAsync() => _catalog.StartLoad(false, true);

    #endregion

    #region Query

    public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var search = query.Search.Trim();

        if (!_catalog.Ready)
        {
            // Give the background load a chance to finish so the common case needs no
            // second keystroke; fall back to a status line when it is still downloading.
            var loading = _catalog.StartLoad(false, false);
            await Task.WhenAny(loading, Task.Delay(2500, CancellationToken.None)).ConfigureAwait(false);

            if (!_catalog.Ready) return StatusResults();
        }

        if (search.StartsWith(DrillPrefix, StringComparison.Ordinal))
            return DrillInResults(query, search[DrillPrefix.Length..]);

        if (search.StartsWith('!'))
            return CommandResults(query, search[1..]);

        return GameResults(query, search);
    }

    private List<Result> StatusResults()
    {
        if (_catalog.Error is { } error)
        {
            return Single("Failed to load game data", error, Icons.Missing, _ =>
            {
                _catalog.StartLoad(true, true);
                _context.API.ShowMsg("Ludex", "Retrying...");
                return false;
            });
        }

        return Single("Loading game database...", _catalog.Status, Icons.App, _ =>
        {
            _context.API.ReQuery();
            return false;
        });
    }

    private List<Result> GameResults(Query query, string search)
    {
        var results = new List<Result>();
        var games = _catalog.Games;

        if (search.Length > 0)
        {
            foreach (var game in games)
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
            ? games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            : games.AsEnumerable();

        var rank = games.Count;
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

        if (!_catalog.TryGetGame(slug, out var game))
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
        foreach (var entry in FolderEntries.Build(game))
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
        _catalog.Begin(caption);

        using var cts = new CancellationTokenSource();

        try
        {
            await _context.API.ShowProgressBoxAsync(
                caption,
                async report =>
                {
                    report(0);
                    await _catalog.StartLoad(manifest, games, report, cts.Token).ConfigureAwait(false);
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
            _catalog.Error is null ? $"Ready — {_catalog.Games.Count} games" : $"Failed: {_catalog.Error}",
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

        foreach (var (name, entry) in _catalog.Index)
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
                var game = _catalog.ManifestOnlyGame(s.Name);
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
                        _catalog.Register(game);
                        _context.API.ChangeQuery($"{ActionKeyword(query)} {DrillPrefix}{game.Slug} ");
                        return false;
                    }
                };
            })
            .ToList();
    }

    #endregion

    #region Folder entries

    private static string FolderIcon(FolderEntry entry)
    {
        if (!entry.Exists) return Icons.Missing;
        if (entry.IsInstall) return Icons.Install;
        return entry.Tags == PathTags.Config ? Icons.Config : Icons.Folder;
    }

    private Result FolderResult(FolderEntry entry, int score)
    {
        var target = FolderEntries.TargetDirectory(entry.Path);
        var fallback = Directory.Exists(target) ? null : FolderEntries.NearestExistingAncestor(target);

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
            IcoPath = FolderIcon(entry),
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
        var target = FolderEntries.TargetDirectory(path);

        if (Directory.Exists(target))
        {
            _context.API.OpenDirectory(target);
            return true;
        }

        // A save folder that does not exist yet is the normal case for a game that has never
        // been launched. Opening the closest existing parent is more useful than an error,
        // and it puts the user where the folder will appear.
        var ancestor = FolderEntries.NearestExistingAncestor(target);
        if (ancestor is null)
        {
            _context.API.ShowMsgError("Folder does not exist", target);
            return false;
        }

        _context.API.OpenDirectory(ancestor);
        _context.API.ShowMsg("Folder not created yet", $"Opened {ancestor} instead.", Icons.Missing);
        return true;
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
                foreach (var entry in FolderEntries.Build(game)) results.Add(FolderResult(entry, score--));
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
