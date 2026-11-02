using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Ludex.Core.Texts;

namespace Ludex;

/// <summary>Text and detail-pane content shared by the game lists.</summary>
internal static class GameDisplay
{
    private const int MaxPathsInDetails = 8;

    public static string StoreLabel(GameStore store) => store switch
    {
        GameStore.Steam => "Steam",
        GameStore.Gog => "GOG",
        GameStore.Epic => "Epic Games",
        GameStore.Xbox => "Xbox",
        _ => store.ToString(),
    };

    private static string SaveCount(int count) => F(count > 1 ? "{0} saves" : "{0} save", count);

    private static string ConfigCount(int count) => F(count > 1 ? "{0} configs" : "{0} config", count);

    public static string Subtitle(GameWithPaths game)
    {
        var counts = new List<string>();
        if (game.SavePaths.Count > 0) counts.Add(SaveCount(game.SavePaths.Count));
        if (game.ConfigPaths.Count > 0) counts.Add(ConfigCount(game.ConfigPaths.Count));
        if (counts.Count == 0) counts.Add(T("no known save paths"));

        var subtitle = string.Join(", ", counts);
        if (!game.Name.Equals(game.DetectedName, StringComparison.OrdinalIgnoreCase))
            subtitle += "  ·  " + F("installed as \"{0}\"", game.DetectedName);

        return subtitle;
    }

    public static IDetails Details(GameWithPaths game)
    {
        var body = new StringBuilder();

        if (!string.IsNullOrEmpty(game.InstallPath))
            body.AppendLine("**" + T("Install folder") + "**\n\n`" + game.InstallPath + "`\n");

        AppendPaths(body, T("Saves"), game.SavePaths);
        AppendPaths(body, T("Config"), game.ConfigPaths);

        if (body.Length == 0) body.Append(T("No known save or config paths."));

        var tags = new List<ITag> { new Tag(StoreLabel(game.Store)) };
        if (game.SavePaths.Count > 0) tags.Add(new Tag(SaveCount(game.SavePaths.Count)));
        if (game.ConfigPaths.Count > 0) tags.Add(new Tag(ConfigCount(game.ConfigPaths.Count)));

        return new Details
        {
            Title = game.Name,
            HeroImage = Icons.ForGame(game.IconPath, game.Matched),
            Body = body.ToString(),
            Metadata = [new DetailsElement { Key = T("Details"), Data = new DetailsTags { Tags = tags.ToArray() } }],
        };
    }

    public static IContextItem[] MoreCommands(LudexApp app, GameWithPaths game)
    {
        var items = new List<IContextItem>();

        if (game.LaunchTarget is { Length: > 0 } target)
            items.Add(new CommandContextItem(new LaunchGameCommand(app, game, target)));

        foreach (var entry in FolderEntries.Build(game))
        {
            items.Add(new CommandContextItem(new OpenFolderCommand(app, game, entry.Path) { Name = entry.Title }));
        }

        return items.ToArray();
    }

    private static void AppendPaths(StringBuilder body, string heading, List<ResolvedPath> paths)
    {
        if (paths.Count == 0) return;

        body.AppendLine("**" + heading + "**\n");

        foreach (var path in paths.Take(MaxPathsInDetails))
            body.AppendLine("- `" + path.Path + "`" + (path.Exists ? "" : "  " + T("(not created yet)")));

        if (paths.Count > MaxPathsInDetails)
            body.AppendLine("- " + F("... and {0} more", paths.Count - MaxPathsInDetails));

        body.AppendLine();
    }
}
