using System.Globalization;
using System.IO;
using System.Windows.Controls;
using Ludex.Core;
using static Ludex.Core.Texts;

namespace Flow.Launcher.Plugin.Ludex;

public class Main : IAsyncPlugin, IContextMenu, ISettingProvider, IAsyncReloadable, IPluginI18n
{
    private const string DrillPrefix = ">";
    private const int ProgressBoxDelayMs = 1500;

    private PluginInitContext _context = null!;
    private Settings _settings = null!;
    private GameCatalog _catalog = null!;

    #region Lifecycle

    public Task InitAsync(PluginInitContext context)
    {
        _context = context;
        Culture = CultureInfo.CurrentUICulture;
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

    public string GetTranslatedPluginTitle() => _context.CurrentPluginMetadata.Name;

    public string GetTranslatedPluginDescription() =>
        T("Browse installed games and open their save, config or install folders");

    public void OnCultureInfoChanged(CultureInfo newCulture) => Culture = newCulture;

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
            return Single(T("Failed to load game data"), error, Icons.Missing, _ =>
            {
                _catalog.StartLoad(true, true);
                _context.API.ShowMsg("Ludex", T("Retrying..."));
                return false;
            });
        }

        return Single(T("Loading game database..."), _catalog.Status, Icons.App, _ =>
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
        if (game.SavePaths.Count > 0)
            counts.Add(F(game.SavePaths.Count > 1 ? "{0} saves" : "{0} save", game.SavePaths.Count));
        if (game.ConfigPaths.Count > 0)
            counts.Add(F(game.ConfigPaths.Count > 1 ? "{0} configs" : "{0} config", game.ConfigPaths.Count));
        if (counts.Count == 0) counts.Add(T("no known save paths"));

        var subtitle = $"{StoreLabel(game.Store)}  ·  {string.Join(", ", counts)}";
        if (!game.Name.Equals(game.DetectedName, StringComparison.OrdinalIgnoreCase))
            subtitle += $"  ·  {F("installed as \"{0}\"", game.DetectedName)}";

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
            return Single(T("Game not found"), T("It may have been removed since the last refresh."), Icons.Missing, _ =>
            {
                _context.API.ChangeQuery($"{ActionKeyword(query)} ");
                return false;
            });

        var results = new List<Result>
        {
            new()
            {
                Title = T("Back to all games"),
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

        // The typed command stays English ("!games"), so match it against the command name too,
        // not only the translated title.
        foreach (var (name, hint) in CommandHintsByName(query))
        {
            if (command.Length == 0 ||
                _context.API.FuzzySearch(command, name).Success ||
                _context.API.FuzzySearch(command, hint.Title).Success)
                results.Add(hint);
        }

        return results;
    }

    private IEnumerable<Result> CommandHints(Query query) => CommandHintsByName(query).Select(h => h.Hint);

    private List<(string Name, Result Hint)> CommandHintsByName(Query query)
    {
        var keyword = ActionKeyword(query);

        return new List<(string, Result)>
        {
            ("games", new Result
            {
                Title = T("Refresh Installed Games"),
                SubTitle = F("{0} !games  ·  re-scan Steam, GOG, Epic and Xbox", keyword),
                IcoPath = Icons.Refresh,
                Score = 3,
                Action = ctx =>
                {
                    _ = RunRefreshAsync(false, true, T("Re-detecting installed games..."));
                    return false;
                }
            }),
            ("refresh", new Result
            {
                Title = T("Refresh Game Database"),
                SubTitle = F("{0} !refresh  ·  re-download the Ludusavi manifest (~16 MB)", keyword),
                IcoPath = Icons.Download,
                Score = 2,
                Action = ctx =>
                {
                    _ = RunRefreshAsync(true, true, T("Downloading the game database..."));
                    return false;
                }
            }),
            ("all", new Result
            {
                Title = T("Search All Games"),
                SubTitle = F("{0} !all <name>  ·  search the whole database, not just installed games", keyword),
                IcoPath = Icons.Search,
                Score = 1,
                Action = _ =>
                {
                    _context.API.ChangeQuery($"{keyword} !all ");
                    return false;
                }
            })
        };
    }

    /// <summary>
    /// Runs a refresh, behind Flow's progress dialog only when it is slow. Called straight from a
    /// result action so the dialog is created on the UI thread; the returned task is intentionally
    /// not awaited.
    /// </summary>
    private async Task RunRefreshAsync(bool manifest, bool games, string caption)
    {
        _catalog.Begin(caption);

        using var cts = new CancellationTokenSource();
        Action<double>? sink = null;
        var latest = 0.0;

        var load = _catalog.StartLoad(manifest, games, p =>
        {
            latest = p;
            sink?.Invoke(p);
        }, cts.Token);

        try
        {
            // A refresh normally takes well under two seconds, and the dialog would only flash
            // and pull focus, which makes Flow hide itself.
            if (await Task.WhenAny(load, Task.Delay(ProgressBoxDelayMs)) != load)
            {
                await _context.API.ShowProgressBoxAsync(
                    caption,
                    async report =>
                    {
                        sink = report;
                        report(latest);
                        await load.ConfigureAwait(false);
                    },
                    cts.Cancel);
            }
        }
        catch (Exception e)
        {
            _context.API.LogException(nameof(Main), "Refresh failed", e);
        }

        if (cts.IsCancellationRequested) return;

        _context.API.ShowMsg(
            "Ludex",
            _catalog.Error is null ? F("Ready — {0} games", _catalog.Games.Count) : F("Failed: {0}", _catalog.Error),
            Icons.App);
    }

    /// <summary>
    /// Searches the whole manifest, including games that are not installed. Paths that
    /// need an install location cannot be resolved here and are dropped by the resolver.
    /// </summary>
    private List<Result> SearchAllResults(Query query, string search)
    {
        if (search.Length < 2)
            return Single(T("Search All Games"), T("Type at least two characters."), Icons.Search, _ => false);

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
                    SubTitle = F("Database entry  ·  {0} save, {1} config paths", game.SavePaths.Count, game.ConfigPaths.Count),
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
            : F("{0}   —   not created yet, opens {1}", entry.Path, fallback);

        var tooltip = entry.Sources.Count > 1
            ? F("{0}\n\nFrom:\n{1}", entry.Path, string.Join('\n', entry.Sources))
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
            Title = T("Launch Game"),
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
                    _context.API.ShowMsgError(T("Could not launch the game"), e.Message);
                    return false;
                }
            }
        };
    }

    private Result NoPathsResult(GameWithPaths game) => new()
    {
        Title = T("No known save or config paths"),
        SubTitle = F("{0} has no usable entry in the Ludusavi manifest.", game.Name),
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
            _context.API.ShowMsgError(T("Folder does not exist"), target);
            return false;
        }

        _context.API.OpenDirectory(ancestor);
        _context.API.ShowMsg(T("Folder not created yet"), F("Opened {0} instead.", ancestor), Icons.Missing);
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
                        Title = T("Copy Path"),
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
                        Title = T("Open Folder"),
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
