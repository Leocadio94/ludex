using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

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

    private static string Count(int count, string noun) =>
        count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count > 1 ? "s" : "");

    public static string Subtitle(GameWithPaths game)
    {
        var counts = new List<string>();
        if (game.SavePaths.Count > 0) counts.Add(Count(game.SavePaths.Count, "save"));
        if (game.ConfigPaths.Count > 0) counts.Add(Count(game.ConfigPaths.Count, "config"));
        if (counts.Count == 0) counts.Add("no known save paths");

        var subtitle = string.Join(", ", counts);
        if (!game.Name.Equals(game.DetectedName, StringComparison.OrdinalIgnoreCase))
            subtitle += $"  ·  installed as \"{game.DetectedName}\"";

        return subtitle;
    }

    public static IDetails Details(GameWithPaths game)
    {
        var body = new StringBuilder();

        if (!string.IsNullOrEmpty(game.InstallPath))
            body.AppendLine("**Install folder**\n\n`" + game.InstallPath + "`\n");

        AppendPaths(body, "Saves", game.SavePaths);
        AppendPaths(body, "Config", game.ConfigPaths);

        if (body.Length == 0) body.Append("No known save or config paths.");

        var tags = new List<ITag> { new Tag(StoreLabel(game.Store)) };
        if (game.SavePaths.Count > 0) tags.Add(new Tag(Count(game.SavePaths.Count, "save")));
        if (game.ConfigPaths.Count > 0) tags.Add(new Tag(Count(game.ConfigPaths.Count, "config")));

        return new Details
        {
            Title = game.Name,
            HeroImage = Icons.ForGame(game.IconPath, game.Matched),
            Body = body.ToString(),
            Metadata = [new DetailsElement { Key = "Details", Data = new DetailsTags { Tags = tags.ToArray() } }],
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
            body.AppendLine("- `" + path.Path + "`" + (path.Exists ? "" : "  (not created yet)"));

        if (paths.Count > MaxPathsInDetails)
            body.AppendLine("- ... and " + (paths.Count - MaxPathsInDetails).ToString(CultureInfo.InvariantCulture) + " more");

        body.AppendLine();
    }
}
