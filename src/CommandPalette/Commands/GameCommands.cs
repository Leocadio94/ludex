using System;
using System.Diagnostics;
using System.IO;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using static Ludex.Core.Texts;

namespace Ludex;

internal sealed partial class LaunchGameCommand : InvokableCommand
{
    private readonly LudexApp _app;
    private readonly GameWithPaths _game;
    private readonly string _target;

    public LaunchGameCommand(LudexApp app, GameWithPaths game, string target)
    {
        _app = app;
        _game = game;
        _target = target;
        Name = T("Launch Game");
        Icon = Icons.Play;
    }

    public override ICommandResult Invoke()
    {
        try
        {
            GameLauncher.Launch(_target, _game.InstallPath);
        }
        catch (Exception e)
        {
            ExtensionHost.LogMessage($"[Ludex] Failed to launch {_game.Name}: {e}");
            return CommandResult.ShowToast(F("Could not launch {0}: {1}", _game.Name, e.Message));
        }

        _app.Usage.Record(_game.Slug);
        return CommandResult.Dismiss();
    }
}

internal sealed partial class OpenFolderCommand : InvokableCommand
{
    private readonly LudexApp _app;
    private readonly GameWithPaths _game;
    private readonly string _path;

    public OpenFolderCommand(LudexApp app, GameWithPaths game, string path)
    {
        _app = app;
        _game = game;
        _path = path;
        Name = T("Open Folder");
        Icon = Icons.Folder;
    }

    public override ICommandResult Invoke()
    {
        var target = FolderEntries.TargetDirectory(_path);

        if (Directory.Exists(target))
        {
            Open(target);
            _app.Usage.Record(_game.Slug);
            return CommandResult.Dismiss();
        }

        // A save folder that does not exist yet is the normal case for a game that has never
        // been launched. Opening the closest existing parent is more useful than an error.
        var ancestor = FolderEntries.NearestExistingAncestor(target);
        if (ancestor is null)
            return CommandResult.ShowToast(F("Folder does not exist: {0}", target));

        Open(ancestor);
        _app.Usage.Record(_game.Slug);
        return CommandResult.ShowToast(F("Folder not created yet. Opened {0} instead.", ancestor));
    }

    private static void Open(string directory) =>
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true })?.Dispose();
}
