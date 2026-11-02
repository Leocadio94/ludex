using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Ludex;

/// <summary>Searches the whole manifest, including games that are not installed.</summary>
internal sealed partial class SearchAllPage : DynamicListPage
{
    private readonly LudexApp _app;
    private string _search = string.Empty;

    public SearchAllPage(LudexApp app)
    {
        _app = app;
        Icon = Icons.Search;
        Title = "Search all games";
        Name = "Search";
        PlaceholderText = "Type a game name (two characters or more)...";
        ShowDetails = true;

        app.Catalog.Changed += () => RaiseItemsChanged();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _search = newSearch.Trim();
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        var catalog = _app.Catalog;
        IsLoading = !catalog.Ready && catalog.Error is null;

        if (!catalog.Ready)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = catalog.Error is null ? "Loading game database..." : "Failed to load game data",
                Subtitle = catalog.Error ?? catalog.Status,
                Icon = Icons.App,
            }];
        }

        if (_search.Length < 2)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = "Search all games",
                Subtitle = "Type at least two characters.",
                Icon = Icons.Search,
            }];
        }

        var scored = new List<(int Score, string Name)>();

        foreach (var (name, entry) in catalog.Index)
        {
            // Alias-only entries would show up as duplicates of their canonical entry.
            if (entry.Files.Count == 0) continue;

            var score = Fuzzy.Score(_search, name);
            if (score > 0) scored.Add((score, name));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(_app.Core.MaxResults)
            .Select(s =>
            {
                var game = catalog.ManifestOnlyGame(s.Name);
                return (IListItem)new ListItem(new GamePage(_app, game))
                {
                    Title = s.Name,
                    Subtitle = $"Database entry  ·  {game.SavePaths.Count} save, {game.ConfigPaths.Count} config paths",
                    Icon = Icons.Search,
                    Details = GameDisplay.Details(game),
                };
            })
            .ToArray();
    }
}
