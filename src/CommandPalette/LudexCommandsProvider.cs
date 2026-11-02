// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Ludex.Core.Texts;

namespace Ludex;

public partial class LudexCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly LudexApp _app = new();

    public LudexCommandsProvider()
    {
        DisplayName = "Ludex";
        Icon = Icons.App;
        Settings = _app.SettingsManager.Settings;

        var searchAll = new SearchAllPage(_app);
        var refreshGames = new RefreshPage(_app, manifest: false);
        var refreshDatabase = new RefreshPage(_app, manifest: true);

        _commands = [
            new CommandItem(new GamesPage(_app))
            {
                Title = DisplayName,
                Subtitle = T("Launch installed games and open their save, config and install folders"),
                MoreCommands = [
                    new CommandContextItem(searchAll),
                    new CommandContextItem(refreshGames),
                    new CommandContextItem(refreshDatabase),
                    new CommandContextItem(_app.SettingsManager.Settings.SettingsPage),
                ],
            },
            new CommandItem(searchAll)
            {
                Title = T("Ludex: Search all games"),
                Subtitle = T("Search the whole Ludusavi database, installed or not"),
            },
            new CommandItem(refreshGames)
            {
                Title = T("Ludex: Refresh installed games"),
                Subtitle = T("Re-scan Steam, GOG, Epic and Xbox"),
            },
            new CommandItem(refreshDatabase)
            {
                Title = T("Ludex: Refresh game database"),
                Subtitle = T("Re-download the Ludusavi manifest (~16 MB)"),
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
