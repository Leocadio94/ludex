// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

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

        _commands = [
            new CommandItem(new GamesPage(_app))
            {
                Title = DisplayName,
                Subtitle = "Launch installed games and open their save, config and install folders",
                MoreCommands = [
                    new CommandContextItem(new SearchAllPage(_app)),
                    new CommandContextItem(new RefreshCommand(_app, manifest: false)),
                    new CommandContextItem(new RefreshCommand(_app, manifest: true)),
                    new CommandContextItem(_app.SettingsManager.Settings.SettingsPage),
                ],
            },
            new CommandItem(new SearchAllPage(_app))
            {
                Title = "Ludex: Search all games",
                Subtitle = "Search the whole Ludusavi database, installed or not",
            },
            new CommandItem(new RefreshCommand(_app, manifest: false))
            {
                Title = "Ludex: Refresh installed games",
                Subtitle = "Re-scan Steam, GOG, Epic and Xbox",
            },
            new CommandItem(new RefreshCommand(_app, manifest: true))
            {
                Title = "Ludex: Refresh game database",
                Subtitle = "Re-download the Ludusavi manifest (~16 MB)",
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
