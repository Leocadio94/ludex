using System.Collections.Generic;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Ludex.Core.Texts;

namespace Ludex;

/// <summary>The actions for one game: launch it, or open one of its folders.</summary>
internal sealed partial class GamePage : ListPage
{
    private readonly LudexApp _app;
    private readonly GameWithPaths _game;

    public GamePage(LudexApp app, GameWithPaths game)
    {
        _app = app;
        _game = game;
        Icon = Icons.ForGame(game.IconPath, game.Matched);
        Title = game.Name;
        Name = T("Open");
        PlaceholderText = T("Filter actions...");
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>();

        if (_game.LaunchTarget is { Length: > 0 } target)
        {
            items.Add(new ListItem(new LaunchGameCommand(_app, _game, target))
            {
                Title = T("Launch Game"),
                Subtitle = GameLauncher.Describe(_game.Store, target),
                Icon = Icons.ForGame(_game.IconPath, _game.Matched),
            });
        }

        foreach (var entry in FolderEntries.Build(_game))
            items.Add(FolderItem(entry));

        if (items.Count == 0)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = T("No known save or config paths"),
                Subtitle = F("{0} has no usable entry in the Ludusavi manifest.", _game.Name),
                Icon = Icons.Missing,
            });
        }

        return items.ToArray();
    }

    private ListItem FolderItem(FolderEntry entry)
    {
        var target = FolderEntries.TargetDirectory(entry.Path);
        var fallback = entry.Exists || System.IO.Directory.Exists(target)
            ? null
            : FolderEntries.NearestExistingAncestor(target);

        // Say up front what Enter will actually do, rather than letting the user press it and
        // watch nothing happen.
        var subtitle = fallback is null
            ? entry.Path
            : F("{0}  -  not created yet, opens {1}", entry.Path, fallback);

        return new ListItem(new OpenFolderCommand(_app, _game, entry.Path))
        {
            Title = entry.Title,
            Subtitle = subtitle,
            Icon = Icons.ForFolder(entry),
            MoreCommands =
            [
                new CommandContextItem(new CopyTextCommand(entry.Path) { Name = T("Copy Path") }),
            ],
        };
    }
}
