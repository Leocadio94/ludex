using System;
using System.Collections.Generic;
using System.Linq;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Ludex.Core.Texts;

namespace Ludex;

internal sealed partial class GamesPage : ListPage
{
    private readonly LudexApp _app;

    public GamesPage(LudexApp app)
    {
        _app = app;
        Icon = Icons.App;
        Title = "Ludex";
        Name = T("Open");
        PlaceholderText = T("Search installed games...");
        ShowDetails = true;

        app.Catalog.Changed += Refresh;
        app.Usage.Changed += Refresh;
        app.SettingsApplied += Refresh;
    }

    private void Refresh() => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        var catalog = _app.Catalog;
        IsLoading = !catalog.Ready && catalog.Error is null;

        if (!catalog.Ready)
            return [StatusItem(catalog)];

        var ordered = _app.Core.DefaultSort == GameSort.Alphabetical
            ? catalog.Games.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            : catalog.Games
                .OrderByDescending(g => _app.Usage.CountFor(g.Slug))
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase);

        return ordered.Select(GameItem).ToArray();
    }

    private ListItem StatusItem(GameCatalog catalog)
    {
        if (catalog.Error is { } error)
        {
            return new ListItem(new RefreshPage(_app, manifest: true))
            {
                Title = T("Failed to load game data"),
                Subtitle = error,
                Icon = Icons.Missing,
            };
        }

        return new ListItem(new NoOpCommand())
        {
            Title = T("Loading game database..."),
            Subtitle = catalog.Status,
            Icon = Icons.App,
        };
    }

    private ListItem GameItem(GameWithPaths game) => new(new GamePage(_app, game))
    {
        Title = game.Name,
        Subtitle = GameDisplay.Subtitle(game),
        Icon = Icons.ForGame(game.IconPath, game.Matched),
        Tags = [new Tag(GameDisplay.StoreLabel(game.Store))],
        Details = GameDisplay.Details(game),
        MoreCommands = GameDisplay.MoreCommands(_app, game),
    };
}
