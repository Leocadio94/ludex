using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ludex.Core;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

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
        Name = "Launch Game";
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
            new ToastStatusMessage($"Could not launch {_game.Name}: {e.Message}").Show();
            return CommandResult.KeepOpen();
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
        Name = "Open Folder";
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
        {
            new ToastStatusMessage($"Folder does not exist: {target}").Show();
            return CommandResult.KeepOpen();
        }

        Open(ancestor);
        _app.Usage.Record(_game.Slug);
        return CommandResult.ShowToast($"Folder not created yet. Opened {ancestor} instead.");
    }

    private static void Open(string directory) =>
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true })?.Dispose();
}

internal sealed partial class RefreshCommand : InvokableCommand
{
    private readonly LudexApp _app;
    private readonly bool _manifest;
    private readonly string _caption;

    public RefreshCommand(LudexApp app, bool manifest)
    {
        _app = app;
        _manifest = manifest;
        _caption = manifest ? "Downloading the game database..." : "Re-detecting installed games...";
        Name = manifest ? "Refresh Game Database" : "Refresh Installed Games";
        Icon = manifest ? Icons.Download : Icons.Refresh;
    }

    public override ICommandResult Invoke()
    {
        _ = RunAsync();
        return CommandResult.KeepOpen();
    }

    private async Task RunAsync()
    {
        var catalog = _app.Catalog;
        catalog.Begin(_caption);

        var progress = new ProgressState { ProgressPercent = 0 };
        var message = new StatusMessage
        {
            Message = _caption,
            State = MessageState.Info,
            Progress = progress,
        };

        ExtensionHost.ShowStatus(message, StatusContext.Extension);

        try
        {
            await catalog.StartLoad(_manifest, true, percent => progress.ProgressPercent = (uint)percent)
                .ConfigureAwait(false);
        }
        finally
        {
            ExtensionHost.HideStatus(message);
        }

        new ToastStatusMessage(
            catalog.Error is null ? $"Ready: {catalog.Games.Count} games" : $"Failed: {catalog.Error}").Show();
    }
}
